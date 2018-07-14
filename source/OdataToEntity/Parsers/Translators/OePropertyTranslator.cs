using Microsoft.OData.Edm;
using OdataToEntity.ModelBuilder;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace OdataToEntity.Parsers.Translators
{
    public sealed class OePropertyTranslator : ExpressionVisitor
    {
        private IEdmProperty _edmProperty;
        private List<Expression> _expressions;
        private PropertyInfo _foundProperty;
        private NewExpression _newExpression;
        private readonly Expression _source;
        private Type _tupleType;

        public OePropertyTranslator(Expression source)
        {
            _source = source;
        }

        public MemberExpression Build(Expression parameter, IEdmProperty edmProperty)
        {
            MemberExpression propertyExpression = CreatePropertyExpression(parameter, edmProperty);
            if (propertyExpression == null)
            {
                PropertyInfo property = _tupleType.GetPropertyIgnoreCase(edmProperty.Name);
                propertyExpression = property == null ? null : Expression.Property(parameter, property);
            }
            return propertyExpression;
        }
        private static bool Compare(MemberExpression expression, IEdmProperty edmProperty)
        {
            if (String.Compare(expression.Member.Name, edmProperty.Name, StringComparison.OrdinalIgnoreCase) != 0)
                return false;

            var schemaElement = (IEdmSchemaElement)edmProperty.DeclaringType;
            return String.Compare(expression.Member.DeclaringType.Name, schemaElement.Name, StringComparison.OrdinalIgnoreCase) == 0 &&
                String.Compare(expression.Member.DeclaringType.Namespace, schemaElement.Namespace, StringComparison.OrdinalIgnoreCase) == 0;
        }
        private MemberExpression CreateFromGrouping(Expression parameter, IEdmProperty edmProperty)
        {
            MemberExpression keyExpression = null;
            Type tupleType = parameter.Type;
            while (tupleType.IsGenericType && tupleType.GetGenericTypeDefinition() == typeof(IGrouping<,>))
            {
                keyExpression = Expression.Property(keyExpression ?? parameter, nameof(IGrouping<Object, Object>.Key));
                tupleType = keyExpression.Type;
            }
            if (keyExpression == null)
                return null;

            int index = 0;
            do
            {
                IReadOnlyList<MemberExpression> properties = OeExpressionHelper.GetPropertyExpressions(keyExpression);
                if (OeExpressionHelper.IsTupleType(keyExpression.Type))
                    return CreatePropertyExpression(keyExpression, edmProperty);

                for (int i = index; i < properties.Count; i++)
                    if (Compare(properties[i], edmProperty))
                        return properties[i];

                index = 1;
                keyExpression = keyExpression.Expression as MemberExpression;
            }
            while (keyExpression != null);

            throw new InvalidOperationException("zzz");
        }
        public MemberExpression CreatePropertyExpression(Expression parameter, IEdmProperty edmProperty)
        {
            _newExpression = null;
            _foundProperty = null;

            _tupleType = parameter.Type;

            Expression propertyExpression2 = CreateFromGrouping(parameter, edmProperty);
            if (propertyExpression2 != null)
                return (MemberExpression)propertyExpression2;

            if (parameter is MemberExpression property2 && !OeExpressionHelper.IsTupleType(_tupleType))
            {
                IReadOnlyList<MemberExpression> nestedProperties = OeExpressionHelper.GetPropertyExpressions(property2);
                for (int i = 0; i < nestedProperties.Count; i++)
                    if (Compare(nestedProperties[i], edmProperty))
                    {
                        _expressions = new List<Expression>() { property2 };
                        return nestedProperties[i];
                    }

                return null;
            }
            else
            {
                base.Visit(_source);
                if (_newExpression == null)
                    return null;

                if (_newExpression.Arguments[0] is ParameterExpression parameterExpression)
                {
                    return FindInFirstArgumentTuple(parameter, parameterExpression, edmProperty);
                }
            }

            _edmProperty = edmProperty;
            _expressions = new List<Expression>() { parameter };
            FindProperty(_newExpression.Arguments);
            if (_foundProperty == null)
                return null;

            Expression propertyExpression = _expressions[0];
            for (int i = 0; i < _expressions.Count; i++)
            {
                PropertyInfo propertyInfo = null;
                if (i < _expressions.Count - 1)
                {
                    foreach (PropertyInfo property in _expressions[i].Type.GetProperties())
                        if (property.PropertyType == _expressions[i + 1].Type)
                        {
                            propertyInfo = property;
                            break;
                        }
                }
                else
                    propertyInfo = _foundProperty;
                propertyExpression = Expression.Property(propertyExpression, propertyInfo);
            }
            return (MemberExpression)propertyExpression;
        }
        private MemberExpression FindInFirstArgumentTuple(Expression parameter, ParameterExpression parameterExpression, IEdmProperty edmProperty)
        {
            MemberExpression item1Property = Expression.Property(parameter, nameof(Tuple<Object>.Item1));

            if (OeExpressionHelper.IsTupleType(parameterExpression.Type))
            {
                var translator = new OePropertyTranslator(_source);
                MemberExpression propertyExpression = translator.CreatePropertyExpression(parameterExpression, edmProperty);
                return ReplaceParameter(propertyExpression, item1Property);
            }

            return Expression.Property(item1Property, edmProperty.Name);
        }
        private void FindProperty(ReadOnlyCollection<Expression> ctorArguments)
        {
            for (int i = 0; i < ctorArguments.Count; i++)
            {
                if (ctorArguments[i] is MemberExpression propertyExpression)
                {
                    if (Compare(propertyExpression, _edmProperty))
                    {
                        _foundProperty = _expressions[_expressions.Count - 1].Type.GetProperties()[i];
                        return;
                    }
                    else
                    {
                        if (!IsPrimitiveType(propertyExpression.Type))
                        {
                            var tupleVisitor = new OePropertyTranslator(_source);
                            propertyExpression = tupleVisitor.CreatePropertyExpression(propertyExpression, _edmProperty);
                            if (propertyExpression != null)
                            {
                                _foundProperty = (PropertyInfo)propertyExpression.Member;
                                _expressions.AddRange(tupleVisitor._expressions);
                                return;
                            }
                        }
                    }
                }
                else
                {
                    if (ctorArguments[i] is NewExpression newExpression)
                    {
                        _expressions.Add(newExpression);
                        VisitNew(newExpression);
                        if (_foundProperty != null)
                            return;
                        _expressions.RemoveAt(_expressions.Count - 1);
                    }
                }
            }
        }
        public static bool IsPrimitiveType(Type clrType)
        {
            if (PrimitiveTypeHelper.GetPrimitiveType(clrType) != null || clrType.IsEnum)
                return true;

            Type underlyingType = Nullable.GetUnderlyingType(clrType);
            if (underlyingType != null && (PrimitiveTypeHelper.GetPrimitiveType(underlyingType) != null || underlyingType.IsEnum))
                return true;

            return false;
        }
        private static MemberExpression ReplaceParameter(MemberExpression propertyExpression, Expression newParameter)
        {
            var stack = new Stack<MemberExpression>();
            do
            {
                stack.Push(propertyExpression);
                propertyExpression = propertyExpression.Expression as MemberExpression;
            }
            while (propertyExpression != null);

            while (stack.Count > 0)
            {
                var property = (PropertyInfo)stack.Pop().Member;
                newParameter = Expression.Property(newParameter, property);
            }

            return (MemberExpression)newParameter;
        }
        protected override Expression VisitNew(NewExpression node)
        {
            if (_newExpression == null)
            {
                if (node.Type == _tupleType)
                {
                    _newExpression = node;
                    return node;
                }
            }
            else
            {
                if (_edmProperty != null)
                {
                    FindProperty(node.Arguments);
                    if (_foundProperty != null)
                        return node;
                }
            }
            return base.VisitNew(node);
        }
    }
}
