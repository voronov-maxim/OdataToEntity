using Microsoft.EntityFrameworkCore;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace OdataToEntity.Test
{
    internal sealed class IncludeVisitor : ExpressionVisitor
    {
        public readonly struct Include
        {
            public Include(PropertyInfo property, Func<IEnumerable, IList> filter, bool isSelect)
            {
                Property = property;
                Filter = filter;
                IsSelect = isSelect;
            }

            public Func<IEnumerable, IList> Filter { get; }
            public bool IsSelect { get; }
            public PropertyInfo Property { get; }
        }

        private sealed class NewVisitor : ExpressionVisitor
        {
            private readonly List<PropertyInfo> _selectProperties;

            public NewVisitor()
            {
                _selectProperties = new List<PropertyInfo>();
            }

            protected override Expression VisitNew(NewExpression node)
            {
                foreach (MemberExpression propertyExpression in node.Arguments.OfType<MemberExpression>())
                {
                    var property = (PropertyInfo)propertyExpression.Member;
                    if (TestContractResolver.IsEntity(property.PropertyType))
                        _selectProperties.Add(property);
                }
                return node;
            }
            public List<PropertyInfo> SelectProperties => _selectProperties;
        }

        private sealed class PropertyVisitor : ExpressionVisitor
        {
            private Func<IEnumerable, IList> _filter;
            private ParameterExpression _parameter;
            private MemberExpression _property;

            protected override Expression VisitMember(MemberExpression node)
            {
                if (_property == null)
                    _property = node;
                return node;
            }
            protected override Expression VisitLambda<T>(Expression<T> node)
            {
                if (_property == null)
                {
                    _parameter = node.Parameters[0];
                    return base.VisitLambda<T>(node);
                }
                return node;
            }
            protected override Expression VisitMethodCall(MethodCallExpression node)
            {
                ParameterExpression source = Expression.Parameter(typeof(IEnumerable));
                UnaryExpression convert = Expression.Convert(source, node.Arguments[0].Type);
                MethodCallExpression call = Expression.Call(null, node.Method, convert, node.Arguments[1]);

                Type itemType = node.Arguments[0].Type.GetGenericArguments()[0];
                Type listType = typeof(List<>).MakeGenericType(itemType);
                ConstructorInfo listCtor = listType.GetConstructor(new[] { typeof(IEnumerable<>).MakeGenericType(itemType) });
                NewExpression list = Expression.New(listCtor, call);
                _filter = Expression.Lambda<Func<IEnumerable, IList>>(list, source).Compile();

                return base.VisitMethodCall(node);
            }

            public Func<IEnumerable, IList> Filter => _filter;
            public ParameterExpression Parameter => _parameter;
            public MemberExpression Property => _property;
        }

        private readonly List<Include> _includes;

        public IncludeVisitor()
        {
            _includes = new List<Include>();
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            Expression expression = base.Visit(node.Arguments[0]);

            if (node.Method.DeclaringType == typeof(EntityFrameworkQueryableExtensions))
            {
                if (node.Method.Name == nameof(EntityFrameworkQueryableExtensions.Include) ||
                    node.Method.Name == nameof(EntityFrameworkQueryableExtensions.ThenInclude))
                {
                    var visitor = new PropertyVisitor();
                    visitor.Visit(node.Arguments[1]);

                    if (visitor.Filter == null)
                        node = Expression.Call(null, node.Method, new Expression[] { expression, node.Arguments[1] });
                    else
                    {
                        if (node.Method.Name == nameof(EntityFrameworkQueryableExtensions.Include))
                        {
                            Type entityType = node.Method.GetGenericArguments()[0];
                            MethodInfo method = node.Method.GetGenericMethodDefinition().MakeGenericMethod(entityType, visitor.Property.Type);
                            LambdaExpression lambda = Expression.Lambda(visitor.Property, visitor.Parameter);
                            node = Expression.Call(null, method, new Expression[] { node.Arguments[0], lambda });
                        }
                        else
                        {
                            Type entityType = node.Method.GetGenericArguments()[0];
                            Type previousPropertyType = node.Method.GetGenericArguments()[1];
                            MethodInfo method = node.Method.GetGenericMethodDefinition().MakeGenericMethod(entityType, previousPropertyType, visitor.Property.Type);
                            LambdaExpression lambda = Expression.Lambda(visitor.Property, visitor.Parameter);
                            node = Expression.Call(null, method, new Expression[] { node.Arguments[0], lambda });
                        }
                    }

                    _includes.Add(new Include(visitor.Property.Member as PropertyInfo, visitor.Filter, false));
                }
            }
            else if (node.Method.DeclaringType == typeof(Queryable) && node.Method.Name == nameof(Queryable.Select))
            {
                var visitor = new NewVisitor();
                visitor.Visit(node.Arguments[1]);
                _includes.AddRange(visitor.SelectProperties.Select(p => new Include(p, null, true)));
            }
            else
            {
                if (node.Arguments.Count == 1)
                    node = Expression.Call(node.Object, node.Method, expression);
                else
                    node = Expression.Call(node.Object, node.Method, expression, node.Arguments[1]);
            }
            return node;
        }

        public IReadOnlyList<Include> Includes => _includes;
    }
}
