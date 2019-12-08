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
        private class DatabaseNullHighestValueComparer<T> : IComparer<T>
        {
            public int Compare(T x, T y)
            {
                if (EqualityComparer<T>.Default.Equals(x, default) || EqualityComparer<T>.Default.Equals(y, default))
                {
                    if (EqualityComparer<T>.Default.Equals(x, default) && EqualityComparer<T>.Default.Equals(y, default))
                        return 0;

                    return EqualityComparer<T>.Default.Equals(x, default) ? 1 : -1;
                }

                return Comparer<T>.Default.Compare(x, y);
            }
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
                    if (Parsers.OeExpressionHelper.IsEntityType(property.PropertyType))
                        SelectProperties.Add(property);
                }
                return node;
            }

            public List<PropertyInfo> SelectProperties { get; }
        }

        private sealed class PropertyVisitor : ExpressionVisitor
        {
            private readonly bool _isDatabaseNullHighestValue;

            public PropertyVisitor(bool isDatabaseNullHighestValue)
            {
                _isDatabaseNullHighestValue = isDatabaseNullHighestValue;
            }

            protected override Expression VisitMember(MemberExpression node)
            {
                if (PropertyExpression == null)
                    PropertyExpression = node;
                return node;
            }
            protected override Expression VisitLambda<T>(Expression<T> node)
            {
                if (PropertyExpression == null)
                {
                    Parameter = node.Parameters[0];
                    return base.VisitLambda<T>(node);
                }
                return node;
            }
            protected override Expression VisitMethodCall(MethodCallExpression node)
            {
                ParameterExpression source = Expression.Parameter(typeof(IEnumerable));
                UnaryExpression convert = Expression.Convert(source, node.Arguments[0].Type);

                MethodCallExpression call = Expression.Call(node.Method, convert, node.Arguments[1]);
                if (node.Method.Name == nameof(Enumerable.OrderBy) || node.Method.Name == nameof(Enumerable.OrderByDescending) ||
                    node.Method.Name == nameof(Enumerable.ThenBy) || node.Method.Name == nameof(Enumerable.ThenByDescending))
                {
                    IsOrdered = true;
                    if (_isDatabaseNullHighestValue)
                    {
                        Type keyType = ((LambdaExpression)node.Arguments[1]).ReturnType;

                        Type sourceType = Parsers.OeExpressionHelper.GetCollectionItemTypeOrNull(node.Arguments[0].Type);
                        MethodInfo method = typeof(Enumerable).GetMethods().Single(m => m.Name == node.Method.Name && m.GetParameters().Length == 3);
                        MethodInfo sortMethod = method.GetGenericMethodDefinition().MakeGenericMethod(new Type[] { sourceType, keyType });

                        Type comparerType = typeof(DatabaseNullHighestValueComparer<>).MakeGenericType(keyType);
                        Object keyComparer = Activator.CreateInstance(comparerType);
                        call = Expression.Call(sortMethod, convert, node.Arguments[1], Expression.Constant(keyComparer));
                    }
                }

                Type itemType = node.Arguments[0].Type.GetGenericArguments()[0];
                Type listType = typeof(List<>).MakeGenericType(itemType);
                ConstructorInfo listCtor = listType.GetConstructor(new[] { typeof(IEnumerable<>).MakeGenericType(itemType) });
                NewExpression list = Expression.New(listCtor, call);
                Filter = Expression.Lambda<Func<IEnumerable, IList>>(list, source).Compile();

                if (node.Method.Name == nameof(EntityFrameworkQueryableExtensions.Include) ||
                    node.Method.Name == nameof(EntityFrameworkQueryableExtensions.ThenInclude))
                    base.Visit(node.Arguments[1]);
                else
                    base.VisitMethodCall(node);

                return node;
            }

            public Func<IEnumerable, IList> Filter { get; private set; }
            public bool IsOrdered { get; private set; }
            public ParameterExpression Parameter { get; private set; }
            public MemberExpression PropertyExpression { get; private set; }
        }

        private readonly List<EfInclude> _includes;
        private readonly bool _isDatabaseNullHighestValue;
        private readonly ModelBuilder.OeEdmModelMetadataProvider _metadataProvider;

        public IncludeVisitor(ModelBuilder.OeEdmModelMetadataProvider metadataProvider, bool isDatabaseNullHighestValue)
        {
            _metadataProvider = metadataProvider;
            _isDatabaseNullHighestValue = isDatabaseNullHighestValue;
            _includes = new List<EfInclude>();
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            Expression expression = base.Visit(node.Arguments[0]);

            if (node.Method.DeclaringType == typeof(EntityFrameworkQueryableExtensions))
            {
                if (node.Method.Name == nameof(EntityFrameworkQueryableExtensions.Include) ||
                    node.Method.Name == nameof(EntityFrameworkQueryableExtensions.ThenInclude))
                {
                    var visitor = new PropertyVisitor(_isDatabaseNullHighestValue);
                    visitor.Visit(node.Arguments[1]);

                    if (_metadataProvider.IsNotMapped((PropertyInfo)visitor.PropertyExpression.Member))
                    {
                        _includes.Add(new EfInclude(visitor.PropertyExpression.Member as PropertyInfo, visitor.Filter, visitor.IsOrdered));
                        return node.Arguments[0];
                    }

                    if (visitor.Filter == null)
                        node = Expression.Call(null, node.Method, new Expression[] { expression, node.Arguments[1] });
                    else
                    {
                        Type entityType = node.Method.GetGenericArguments()[0];
                        MethodInfo method;
                        if (node.Method.Name == nameof(EntityFrameworkQueryableExtensions.Include))
                            method = node.Method.GetGenericMethodDefinition().MakeGenericMethod(entityType, visitor.PropertyExpression.Type);
                        else
                        {
                            Type previousPropertyType = node.Method.GetGenericArguments()[1];
                            method = node.Method.GetGenericMethodDefinition().MakeGenericMethod(entityType, previousPropertyType, visitor.PropertyExpression.Type);
                        }
                        LambdaExpression lambda = Expression.Lambda(visitor.PropertyExpression, visitor.Parameter);
                        node = Expression.Call(null, method, new Expression[] { expression, lambda });
                    }

                    PropertyInfo parentProperty = null;
                    if (node.Method.Name == nameof(EntityFrameworkQueryableExtensions.ThenInclude))
                    {
                        var parentVisitor = new PropertyVisitor(_isDatabaseNullHighestValue);
                        parentVisitor.Visit(node.Arguments[0]);
                        parentProperty = parentVisitor.PropertyExpression.Member as PropertyInfo;
                    }

                    _includes.Add(new EfInclude(visitor.PropertyExpression.Member as PropertyInfo, visitor.Filter, visitor.IsOrdered, parentProperty));
                    return node;
                }
            }
            else if (node.Method.DeclaringType == typeof(Queryable) && node.Method.Name == nameof(Queryable.Select))
            {
                var visitor = new NewVisitor();
                visitor.Visit(node.Arguments[1]);
                _includes.AddRange(visitor.SelectProperties.Select(p => new EfInclude(p, null, false)));
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
                    _includes.Add(new EfInclude(navigationProperty, null, false));
                }
                else if (navigationProperties.Count == 1)
                {
                    _includes.Add(new EfInclude(navigationProperties[0], null, false));
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
                        _includes.Add(new EfInclude(navigationProperty, null, false));
                    }
                    else if (outerKeySelector.Body is NewExpression newExpression)
                    {
                        List<PropertyInfo> properties = newExpression.Arguments.Select(a => (PropertyInfo)((MemberExpression)a).Member).ToList();
                        foreach (PropertyInfo navigationProperty in navigationProperties)
                        {
                            PropertyInfo[] structuralProperties = _metadataProvider.GetForeignKey(navigationProperty);
                            if (!structuralProperties.Except(properties).Any())
                            {
                                _includes.Add(new EfInclude(navigationProperty, null, false));
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

        public IReadOnlyList<EfInclude> Includes => _includes;
    }
}
