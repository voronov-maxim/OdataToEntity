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

        private static bool Compare(MemberExpression expression, IEdmProperty edmProperty)
        {
            if (String.Compare(expression.Member.Name, edmProperty.Name, StringComparison.OrdinalIgnoreCase) != 0)
                return false;

            var schemaElement = (IEdmSchemaElement)edmProperty.DeclaringType;
            return String.Compare(expression.Member.DeclaringType.Name, schemaElement.Name, StringComparison.OrdinalIgnoreCase) == 0 &&
                String.Compare(expression.Member.DeclaringType.Namespace, schemaElement.Namespace, StringComparison.OrdinalIgnoreCase) == 0;
        }
        public MemberExpression Build(Expression parameter, IEdmProperty edmProperty)
        {
            _newExpression = null;
            _foundProperty = null;

            _tupleType = parameter.Type;

            base.Visit(_source);
            if (_newExpression == null)
                return null;

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

                    if (!OeExpressionHelper.IsPrimitiveType(propertyExpression.Type))
                    {
                        var tupleVisitor = new OePropertyTranslator(_source);
                        propertyExpression = tupleVisitor.Build(propertyExpression, _edmProperty);
                        if (propertyExpression != null)
                        {
                            _foundProperty = (PropertyInfo)propertyExpression.Member;
                            _expressions.AddRange(tupleVisitor._expressions);
                            return;
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
