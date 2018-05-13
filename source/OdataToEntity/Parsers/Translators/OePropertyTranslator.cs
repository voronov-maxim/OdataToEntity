using Microsoft.OData.Edm;
using OdataToEntity.ModelBuilder;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq.Expressions;
using System.Reflection;

namespace OdataToEntity.Parsers
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
        public MemberExpression CreatePropertyExpression(Expression parameter, IEdmProperty edmProperty)
        {
            _newExpression = null;
            _foundProperty = null;

            _tupleType = parameter.Type;
            base.Visit(_source);
            if (_newExpression == null)
                return null;

            if (_newExpression.Arguments[0].NodeType == ExpressionType.Parameter)
            {
                PropertyInfo property = _newExpression.Arguments[0].Type.GetPropertyIgnoreCase(edmProperty.Name);
                if (property != null)
                {
                    MemberExpression item1 = Expression.Property(parameter, _newExpression.Type.GetProperty("Item1"));
                    return Expression.Property(item1, property);
                }
            }

            _edmProperty = edmProperty;
            _expressions = new List<Expression>() { parameter };
            FindProperty(_newExpression.Arguments);

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
                    if (String.Compare(propertyExpression.Member.Name, _edmProperty.Name, StringComparison.OrdinalIgnoreCase) == 0 &&
                        String.Compare(propertyExpression.Member.DeclaringType.FullName, _edmProperty.DeclaringType.FullTypeName(), StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        _foundProperty = _expressions[_expressions.Count - 1].Type.GetProperties()[i];
                        return;
                    }
                    else
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
