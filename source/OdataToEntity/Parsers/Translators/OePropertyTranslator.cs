using Microsoft.OData.Edm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq.Expressions;
using System.Reflection;

namespace OdataToEntity.Parsers.Translators
{
    public sealed class OePropertyTranslator : ExpressionVisitor
    {
        private readonly struct PropertyDef
        {
            public PropertyDef(String namespaceName, String declaringTypeName, String propertyName)
            {
                NamespaceName = namespaceName;
                DeclaringTypeName = declaringTypeName;
                PropertyName = propertyName;
            }

            public String DeclaringTypeName { get; }
            public String NamespaceName { get; }
            public String PropertyName { get; }
        }

        private readonly List<Expression> _expressions;
        private PropertyInfo? _foundProperty;
        private PropertyDef _propertyDef;
        private readonly Expression _source;
        private Type _tupleType;

        public OePropertyTranslator(Expression source)
        {
            _source = source;
            _expressions = new List<Expression>();
            _tupleType = typeof(Object);
        }

        private static bool Compare(MemberExpression propertyExpression, in PropertyDef propertyDef)
        {
            Type declaringType = propertyExpression.Member.DeclaringType!;
            for (; ; )
            {
                if (String.Compare(declaringType.Name, propertyDef.DeclaringTypeName, StringComparison.OrdinalIgnoreCase) == 0 &&
                    String.Compare(declaringType.Namespace, propertyDef.NamespaceName, StringComparison.OrdinalIgnoreCase) == 0 &&
                    String.Compare(propertyExpression.Member.Name, propertyDef.PropertyName, StringComparison.OrdinalIgnoreCase) == 0)
                    return true;

                if (declaringType.BaseType == null)
                    return false;

                declaringType = declaringType.BaseType;
            }
        }
        public MemberExpression? Build(Expression parameter, PropertyInfo propertyInfo)
        {
            var propertyDef = new PropertyDef(propertyInfo.DeclaringType!.Namespace ?? "", propertyInfo.DeclaringType.Name, propertyInfo.Name);
            return Build(parameter, propertyDef);
        }
        public MemberExpression? Build(Expression parameter, IEdmProperty edmProperty)
        {
            var schemaElement = (IEdmSchemaElement)edmProperty.DeclaringType;
            var propertyDef = new PropertyDef(schemaElement.Namespace, schemaElement.Name, edmProperty.Name);
            return Build(parameter, propertyDef);
        }
        private MemberExpression? Build(Expression parameter, in PropertyDef propertyDef)
        {
            _foundProperty = null;
            _tupleType = parameter.Type;
            _propertyDef = propertyDef;
            _expressions.Add(parameter);

            base.Visit(_source);
            if (_foundProperty == null)
            {
                _foundProperty = GetPropertyInfo(parameter.Type, _propertyDef);
                if (_foundProperty != null)
                    return Expression.Property(parameter, _foundProperty);

                return null;
            }

            Expression propertyExpression = _expressions[0];
            for (int i = 0; i < _expressions.Count; i++)
            {
                PropertyInfo? propertyInfo = null;
                if (i < _expressions.Count - 1)
                {
                    foreach (PropertyInfo property in _expressions[i].Type.GetProperties())
                        if (property.PropertyType == _expressions[i + 1].Type)
                        {
                            propertyInfo = property;
                            break;
                        }

                    if (propertyInfo == null)
                        throw new InvalidOperationException("Cannot build navigation properties chain");
                }
                else
                    propertyInfo = _foundProperty;
                propertyExpression = Expression.Property(propertyExpression, propertyInfo);
            }
            return (MemberExpression)propertyExpression;
        }
        private bool FindProperty(Expression expression)
        {
            if (!OeExpressionHelper.IsPrimitiveType(expression.Type))
                if (OeExpressionHelper.IsTupleType(expression.Type))
                {
                    var propertyTranslator = new OePropertyTranslator(_source);
                    MemberExpression? propertyExpression = propertyTranslator.Build(expression, _propertyDef);
                    if (propertyExpression != null)
                    {
                        _foundProperty = (PropertyInfo)propertyExpression.Member;
                        _expressions.AddRange(propertyTranslator._expressions);
                        return true;
                    }
                }
                else
                {
                    _foundProperty = GetPropertyInfo(expression.Type, _propertyDef);
                    if (_foundProperty != null)
                    {
                        _expressions.Add(expression);
                        return true;
                    }
                }

            return false;
        }
        private bool FindProperty(ReadOnlyCollection<Expression> ctorArguments)
        {
            for (int i = 0; i < ctorArguments.Count; i++)
            {
                if (ctorArguments[i] is MemberExpression propertyExpression)
                {
                    if (Compare(propertyExpression, _propertyDef))
                    {
                        _foundProperty = _expressions[_expressions.Count - 1].Type.GetProperties()[i];
                        return true;
                    }

                    if (FindProperty(propertyExpression))
                        return true;
                }
                else if (ctorArguments[i] is NewExpression newExpression)
                {
                    _expressions.Add(newExpression);
                    if (FindProperty(newExpression.Arguments))
                        return true;

                    _expressions.RemoveAt(_expressions.Count - 1);
                }
                else if (ctorArguments[i] is ParameterExpression parameterExpression && FindProperty(parameterExpression))
                    return true;
            }

            return false;
        }
        private static PropertyInfo? GetPropertyInfo(Type type, in PropertyDef propertyDef)
        {
            for (; ; )
            {
                if (String.Compare(type.Name, propertyDef.DeclaringTypeName, StringComparison.OrdinalIgnoreCase) == 0 &&
                    String.Compare(type.Namespace, propertyDef.NamespaceName, StringComparison.OrdinalIgnoreCase) == 0)
                    return type.GetPropertyIgnoreCase(propertyDef.PropertyName);

                if (type.BaseType == null)
                    return null;

                type = type.BaseType;
            }
        }
        protected override Expression VisitNew(NewExpression node)
        {
            if (_foundProperty != null)
                return node;

            if (node.Type == _tupleType && FindProperty(node.Arguments))
                return node;

            return base.VisitNew(node);
        }
    }
}
