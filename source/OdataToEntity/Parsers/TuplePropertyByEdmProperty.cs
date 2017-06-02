using Microsoft.OData.Edm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq.Expressions;
using System.Reflection;

namespace OdataToEntity.Parsers
{
    public sealed class TuplePropertyByEdmProperty : ExpressionVisitor
    {
        private IEdmProperty _edmProperty;
        private NewExpression _newExpression;
        private Stack<Expression> _parameters;
        private MemberExpression _propertyExpression;
        private readonly Expression _source;
        private Type _tupleType;

        public TuplePropertyByEdmProperty(Expression source)
        {
            _source = source;
        }

        public Expression GetTuplePropertyByEdmProperty(Expression parameter, IEdmProperty edmProperty)
        {
            _newExpression = null;
            _propertyExpression = null;

            _tupleType = parameter.Type;
            base.Visit(_source);

            if (_newExpression == null)
                return null;

            _edmProperty = edmProperty;
            _parameters = new Stack<Expression>();

            _parameters.Push(parameter);
            FindProperty(_newExpression.Arguments);
            _parameters.Pop();

            return _propertyExpression;
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
                FindProperty(node.Arguments);
                if (_propertyExpression != null)
                    return node;
            }
            return base.VisitNew(node);
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

                    _parameters.Push(newExpression);
                    VisitNew(newExpression);
                    _parameters.Pop();

                    if (_propertyExpression != null)
                    {
                        PropertyInfo propertyInfo = Parameter.Type.GetTypeInfo().GetProperties()[i];
                        propertyExpression = Expression.Property(Parameter, propertyInfo);
                        _propertyExpression = Expression.Property(propertyExpression, (PropertyInfo)_propertyExpression.Member);
                        break;
                    }
                }
                else if (String.Compare(propertyExpression.Member.Name, _edmProperty.Name, StringComparison.OrdinalIgnoreCase) == 0 &&
                    String.Compare(propertyExpression.Member.DeclaringType.FullName, _edmProperty.DeclaringType.FullTypeName(), StringComparison.OrdinalIgnoreCase) == 0)
                {
                    PropertyInfo propertyInfo = Parameter.Type.GetTypeInfo().GetProperties()[i];
                    _propertyExpression = Expression.Property(Parameter, propertyInfo);
                    break;
                }
            }
        }

        private Expression Parameter => _parameters.Peek();
    }
}
