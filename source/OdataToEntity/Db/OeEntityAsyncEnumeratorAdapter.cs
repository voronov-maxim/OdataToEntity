using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using OdataToEntity.Parsers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity.Db
{
    public sealed class OeEntityAsyncEnumeratorAdapter<T> : IAsyncEnumerator<T>, IAsyncEnumerable<T>
    {
        private CancellationToken _cancellationToken;
        [AllowNull]
        private T _current;
        private readonly OeDbEnumerator _dbEnumerator;
        private bool _isFirstMoveNext;
        private bool _isMoveNext;
        private readonly OeQueryContext? _queryContext;

        public OeEntityAsyncEnumeratorAdapter(IAsyncEnumerator<Object?> asyncEnumerator, OeQueryContext queryContext)
            : this(asyncEnumerator, queryContext.EntryFactory ?? throw new InvalidOperationException("queryContext.EntryFactory is null"), queryContext)
        {
        }
        public OeEntityAsyncEnumeratorAdapter(IAsyncEnumerator<Object> asyncEnumerator, OeEntryFactory entryFactory)
            : this(asyncEnumerator, entryFactory, null)
        {
        }
        private OeEntityAsyncEnumeratorAdapter(IAsyncEnumerator<Object?> asyncEnumerator, OeEntryFactory entryFactory, OeQueryContext? queryContext)
        {
            _dbEnumerator = new OeDbEnumerator(asyncEnumerator, entryFactory);
            _queryContext = queryContext;
            _isFirstMoveNext = true;
            _current = default;
        }

        private static async Task<Object> CreateEntity(IOeDbEnumerator dbEnumerator, Object value, Object entity, Type entityType, CancellationToken cancellationToken)
        {
            if (OeExpressionHelper.IsTupleType(entity.GetType()))
            {
                value = entity;
                entity = CreateEntityFromTuple(entityType, entity, dbEnumerator.EntryFactory.Accessors);
            }

            foreach (OeNavigationEntryFactory navigationLink in dbEnumerator.EntryFactory.NavigationLinks)
                await SetNavigationProperty(dbEnumerator.CreateChild(navigationLink, cancellationToken), value, entity, cancellationToken).ConfigureAwait(false);

            return entity;
        }
        private static Object CreateEntityFromTuple(Type entityType, Object tuple, OePropertyAccessor[] accessors)
        {
            Object entity = Activator.CreateInstance(entityType)!;
            for (int i = 0; i < accessors.Length; i++)
            {
                OePropertyAccessor accessor = accessors[i];
                Object? value = accessor.GetValue(tuple);
                entityType.GetPropertyIgnoreCase(accessor.EdmProperty.Name).SetValue(entity, value);
            }
            return entity;
        }
        private static async Task<Object?> CreateNestedEntity(IOeDbEnumerator dbEnumerator, Object value, Type nestedEntityType, CancellationToken cancellationToken)
        {
            Object? entity = dbEnumerator.Current;
            if (entity == null)
                return null;

            var entryFactory = (OeNavigationEntryFactory)dbEnumerator.EntryFactory;
            if (entryFactory.EdmNavigationProperty.Type.Definition is EdmCollectionType)
            {
                Type listType = typeof(List<>).MakeGenericType(new[] { nestedEntityType });
                var list = (IList)Activator.CreateInstance(listType)!;

                do
                {
                    Object? item = dbEnumerator.Current;
                    if (item != null)
                        list.Add(await CreateEntity(dbEnumerator, item, item, nestedEntityType, cancellationToken).ConfigureAwait(false));
                }
                while (await dbEnumerator.MoveNextAsync().ConfigureAwait(false));
                return list;
            }

            return await CreateEntity(dbEnumerator, value, entity, nestedEntityType, cancellationToken).ConfigureAwait(false);
        }
        public ValueTask DisposeAsync()
        {
            return _dbEnumerator.DisposeAsync();
        }
        IAsyncEnumerator<T> IAsyncEnumerable<T>.GetAsyncEnumerator(CancellationToken cancellationToken)
        {
            _cancellationToken = cancellationToken;
            if (_isFirstMoveNext)
                return this;

            throw new InvalidOperationException("Already iterated");
        }
        ValueTask<bool> IAsyncEnumerator<T>.MoveNextAsync()
        {
            return MoveNextAsync();
        }
        public async ValueTask<bool> MoveNextAsync()
        {
            if (_isFirstMoveNext)
            {
                _isFirstMoveNext = false;
                _isMoveNext = await _dbEnumerator.MoveNextAsync().ConfigureAwait(false);
            }
            if (!_isMoveNext)
                return false;

            T entity = default;
            bool isEntityNull = true;
            if (_dbEnumerator.Current != null)
            {
                isEntityNull = false;
                entity = (T)await CreateEntity(_dbEnumerator, _dbEnumerator.Current, _dbEnumerator.Current, typeof(T), _cancellationToken).ConfigureAwait(false);
            }

            Object? rawValue = _dbEnumerator.RawValue;
            _dbEnumerator.ClearBuffer();

            _isMoveNext = await _dbEnumerator.MoveNextAsync().ConfigureAwait(false);
            if (!isEntityNull && !_isMoveNext &&
                _queryContext != null && _queryContext.EntryFactory != null && _queryContext.EntryFactory.SkipTokenAccessors.Length > 0)
                SetOrderByProperties(_queryContext.EntryFactory, _queryContext.ODataUri.OrderBy, entity!, rawValue);

            _current = entity;
            return true;
        }
        private static async Task SetNavigationProperty(IOeDbEnumerator dbEnumerator, Object value, Object entity, CancellationToken cancellationToken)
        {
            var entryFactory = (OeNavigationEntryFactory)dbEnumerator.EntryFactory;
            PropertyInfo propertyInfo = entity.GetType().GetPropertyIgnoreCase(entryFactory.EdmNavigationProperty.Name);
            Type nestedEntityType = OeExpressionHelper.GetCollectionItemTypeOrNull(propertyInfo.PropertyType) ?? propertyInfo.PropertyType;
            Object? navigationValue = await CreateNestedEntity(dbEnumerator, value, nestedEntityType, cancellationToken).ConfigureAwait(false);
            propertyInfo.SetValue(entity, navigationValue);
        }
        private static void SetOrderByProperties(OeEntryFactory entryFactory, OrderByClause? orderByClause, Object entity, Object? value)
        {
            int i = 0;
            while (orderByClause != null)
            {
                var propertyAccessNode = (SingleValuePropertyAccessNode)orderByClause.Expression;
                var properties = new List<IEdmProperty>() { propertyAccessNode.Property };
                if (propertyAccessNode.Source is SingleNavigationNode navigationNode)
                {
                    properties.Add(navigationNode.NavigationProperty);
                    while (navigationNode.Source is SingleNavigationNode node)
                    {
                        navigationNode = node;
                        properties.Add(navigationNode.NavigationProperty);
                    }
                }

                Object? orderValue = entryFactory.SkipTokenAccessors[i++].GetValue(value);
                SetPropertyValue(entity, properties, orderValue);

                orderByClause = orderByClause.ThenBy;
            }
        }
        private static void SetPropertyValue(Object entity, List<IEdmProperty> edmProperties, Object? orderValue)
        {
            PropertyInfo clrProperty;
            for (int i = edmProperties.Count - 1; i > 0; i--)
            {
                clrProperty = entity.GetType().GetPropertyIgnoreCase(edmProperties[i]);
                Object? navigationValue = clrProperty.GetValue(entity);
                if (navigationValue == null)
                {
                    navigationValue = Activator.CreateInstance(clrProperty.PropertyType)!;
                    clrProperty.SetValue(entity, navigationValue);
                }
                entity = navigationValue;
            }

            clrProperty = entity.GetType().GetPropertyIgnoreCase(edmProperties[0]);
            clrProperty.SetValue(entity, orderValue);
        }

        public Object? Current => _current;
        T IAsyncEnumerator<T>.Current => _current;
    }
}
