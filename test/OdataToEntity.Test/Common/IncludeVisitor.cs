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
            public Include(PropertyInfo property, Func<IEnumerable, IList> filter)
            {
                Property = property;
                Filter = filter;
            }

            public Func<IEnumerable, IList> Filter { get; }
            public PropertyInfo Property { get; }
        }

        private sealed class NewVisitor : ExpressionVisitor
        {
            public NewVisitor()
            {
                SelectProperties = new List<PropertyInfo>();
            }

            protected override Expression VisitNew(NewExpression node)
            {
                foreach (MemberExpression propertyExpression in node.Arguments.OfType<MemberExpression>())
                {
                    var property = (PropertyInfo)propertyExpression.Member;
                    if (TestContractResolver.IsEntity(property.PropertyType))
                        SelectProperties.Add(property);
                }
                return node;
            }

            public List<PropertyInfo> SelectProperties { get; }
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
        private readonly ModelBuilder.OeEdmModelMetadataProvider _metadataProvider;

        public IncludeVisitor(ModelBuilder.OeEdmModelMetadataProvider metadataProvider)
        {
            _metadataProvider = metadataProvider;
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

                    _includes.Add(new Include(visitor.Property.Member as PropertyInfo, visitor.Filter));
                    return node;
                }
            }
            else if (node.Method.DeclaringType == typeof(Queryable) && node.Method.Name == nameof(Queryable.Select))
            {
                var visitor = new NewVisitor();
                visitor.Visit(node.Arguments[1]);
                _includes.AddRange(visitor.SelectProperties.Select(p => new Include(p, null)));
            }
            else if (node.Method.DeclaringType == typeof(Queryable) && node.Method.Name == nameof(Queryable.GroupJoin))
            {
                Type outerType = node.Arguments[0].Type.GetGenericArguments()[0];
                Type innerType = node.Arguments[1].Type.GetGenericArguments()[0];

                List<PropertyInfo> navigationProperties = outerType.GetProperties().Where(p => p.PropertyType == innerType).ToList();
                if (navigationProperties.Count == 0)
                {
                    Type collectionType = typeof(IEnumerable<>).MakeGenericType(innerType);
                    PropertyInfo navigationProperty = outerType.GetProperties().Where(p => collectionType.IsAssignableFrom(p.PropertyType)).Single();
                    _includes.Add(new Include(navigationProperty, null));
                }
                else if (navigationProperties.Count == 1)
                {
                    _includes.Add(new Include(navigationProperties[0], null));
                }
                else
                {
                    LambdaExpression outerKeySelector;
                    if (node.Arguments[2] is UnaryExpression unaryExpression)
                        outerKeySelector = (LambdaExpression)unaryExpression.Operand;
                    else
                        outerKeySelector = (LambdaExpression)node.Arguments[2];

                    if (outerKeySelector.Body is MemberExpression propertyExpression)
                    {
                        PropertyInfo navigationProperty = _metadataProvider.GetForeignKey((PropertyInfo)propertyExpression.Member).Single();
                        _includes.Add(new Include(navigationProperty, null));
                    }
                    else if (outerKeySelector.Body is NewExpression newExpression)
                    {
                        List<PropertyInfo> properties = newExpression.Arguments.Select(a => (PropertyInfo)((MemberExpression)a).Member).ToList();
                        foreach (PropertyInfo navigationProperty in navigationProperties)
                        {
                            PropertyInfo[] structuralProperties = _metadataProvider.GetForeignKey(navigationProperty);
                            if (!structuralProperties.Except(properties).Any())
                            {
                                _includes.Add(new Include(navigationProperty, null));
                                break;
                            }
                        }
                    }
                }
            }

            var arguments = new Expression[node.Arguments.Count];
            node.Arguments.CopyTo(arguments, 0);
            arguments[0] = expression;
            return Expression.Call(node.Object, node.Method, arguments);
        }

        public IReadOnlyList<Include> Includes => _includes;
    }
}
