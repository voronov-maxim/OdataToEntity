using Microsoft.OData.Edm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace OdataToEntity.Parsers
{
    public sealed class TuplePropertyByEdmProperty : ExpressionVisitor
    {
        private IEdmProperty _edmProperty;
        private List<Expression> _expressions;
        private PropertyInfo _foundProperty;
        private NewExpression _newExpression;
        private readonly Expression _source;
        private Type _tupleType;

        public TuplePropertyByEdmProperty(Expression source)
        {
            _source = source;
        }

        private void FindProperty(ReadOnlyCollection<Expression> ctorArguments)
        {
            for (int i = 0; i < ctorArguments.Count; i++)
            {
                var propertyExpression = ctorArguments[i] as MemberExpression;
                if (propertyExpression == null)
                {
                    var newExpression = ctorArguments[i] as NewExpression;
                    if (newExpression == null)
                        continue;

                    _expressions.Add(newExpression);
                    VisitNew(newExpression);
                    if (_foundProperty != null)
                        return;
                    _expressions.RemoveAt(_expressions.Count - 1);
                }
                else if (String.Compare(propertyExpression.Member.Name, _edmProperty.Name, StringComparison.OrdinalIgnoreCase) == 0 &&
                    String.Compare(propertyExpression.Member.DeclaringType.FullName, _edmProperty.DeclaringType.FullTypeName(), StringComparison.OrdinalIgnoreCase) == 0)
                {
                    _foundProperty = _expressions[_expressions.Count - 1].Type.GetProperties()[i];
                    return;
                }
                else
                {
                    var tupleVisitor = new TuplePropertyByEdmProperty(_source);
                    propertyExpression = tupleVisitor.GetTuplePropertyByEdmProperty(propertyExpression, _edmProperty);
                    if (propertyExpression != null)
                    {
                        _foundProperty = (PropertyInfo)propertyExpression.Member;
                        _expressions.AddRange(tupleVisitor._expressions);
                        return;
                    }
                }
            }
        }
        public MemberExpression GetTuplePropertyByEdmProperty(Expression parameter, IEdmProperty edmProperty)
        {
            _newExpression = null;
            _foundProperty = null;

            _tupleType = parameter.Type;
            base.Visit(_source);

            if (_newExpression == null)
                return null;

            _edmProperty = edmProperty;
            _expressions = new List<Expression>();

            _expressions.Add(parameter);
            FindProperty(_newExpression.Arguments);

            if (_foundProperty == null)
                return null;

            Expression propertyExpression = _expressions[0];
            for (int i = 0; i < _expressions.Count; i++)
            {
                PropertyInfo propertyInfo;
                if (i < _expressions.Count - 1)
                {
                    PropertyInfo[] properties = _expressions[i].Type.GetTypeInfo().GetProperties();
                    propertyInfo = properties[properties.Length - 1];
                }
                else
                    propertyInfo = _foundProperty;
                propertyExpression = Expression.Property(propertyExpression, propertyInfo);
            }
            return (MemberExpression)propertyExpression;
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
