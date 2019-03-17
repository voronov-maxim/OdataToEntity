using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using OdataToEntity.Parsers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity.Db
{
    public sealed class OeEntityAsyncEnumeratorAdapter<T> : OeAsyncEnumerator, IAsyncEnumerator<T>, IAsyncEnumerable<T>
    {
        private readonly IDisposable _asyncEnumerator;
        private T _current;
        private readonly OeDbEnumerator _dbEnumerator;
        private bool _isFirstMoveNext;
        private bool _isMoveNext;
        private readonly OeQueryContext _queryContext;

        public OeEntityAsyncEnumeratorAdapter(OeAsyncEnumerator asyncEnumerator, OeQueryContext queryContext)
            : this(asyncEnumerator, queryContext.EntryFactory, queryContext)
        {
        }
        public OeEntityAsyncEnumeratorAdapter(OeAsyncEnumerator asyncEnumerator, OeEntryFactory entryFactory)
            : this(asyncEnumerator, entryFactory, null)
        {
        }
        private OeEntityAsyncEnumeratorAdapter(OeAsyncEnumerator asyncEnumerator, OeEntryFactory entryFactory, OeQueryContext queryContext)
            : base(asyncEnumerator.CancellationToken)
        {
            _asyncEnumerator = asyncEnumerator;
            _dbEnumerator = new OeDbEnumerator(asyncEnumerator, entryFactory);
            _queryContext = queryContext;
            _isFirstMoveNext = true;
            base.Count = asyncEnumerator.Count;
        }

        private static async Task<Object> CreateEntity(IOeDbEnumerator dbEnumerator, Object value, Object entity, Type entityType)
        {
            if (OeExpressionHelper.IsTupleType(entity.GetType()))
            {
                value = entity;
                entity = CreateEntityFromTuple(entityType, entity, dbEnumerator.EntryFactory.Accessors);
            }

            foreach (OeEntryFactory navigationLink in dbEnumerator.EntryFactory.NavigationLinks)
                await SetNavigationProperty(dbEnumerator.CreateChild(navigationLink), value, entity).ConfigureAwait(false);

            return entity;
        }
        private static Object CreateEntityFromTuple(Type entityType, Object tuple, OePropertyAccessor[] accessors)
        {
            Object entity = Activator.CreateInstance(entityType);
            for (int i = 0; i < accessors.Length; i++)
            {
                OePropertyAccessor accessor = accessors[i];
                Object value = accessor.GetValue(tuple);
                entityType.GetProperty(accessor.EdmProperty.Name).SetValue(entity, value);
            }
            return entity;
        }
        private static async Task<Object> CreateNestedEntity(IOeDbEnumerator dbEnumerator, Object value, Type nestedEntityType)
        {
            Object entity = dbEnumerator.Current;
            if (entity == null)
                return null;

            if (dbEnumerator.EntryFactory.EdmNavigationProperty.Type.Definition is EdmCollectionType)
            {
                Type listType = typeof(List<>).MakeGenericType(new[] { nestedEntityType });
                var list = (IList)Activator.CreateInstance(listType);

                do
                {
                    Object item = dbEnumerator.Current;
                    if (item != null)
                        list.Add(await CreateEntity(dbEnumerator, item, item, nestedEntityType).ConfigureAwait(false));
                }
                while (await dbEnumerator.MoveNextAsync().ConfigureAwait(false));
                return list;
            }

            return await CreateEntity(dbEnumerator, value, entity, nestedEntityType).ConfigureAwait(false);
        }
        public override void Dispose()
        {
            _asyncEnumerator.Dispose();
        }
        IAsyncEnumerator<T> IAsyncEnumerable<T>.GetEnumerator()
        {
            if (_isFirstMoveNext)
                return this;

            throw new InvalidOperationException("Already iterated");
        }
        Task<bool> IAsyncEnumerator<T>.MoveNext(CancellationToken cancellationToken)
        {
            return MoveNextAsync();
        }
        public override async Task<bool> MoveNextAsync()
        {
            if (_isFirstMoveNext)
            {
                _isFirstMoveNext = false;
                _isMoveNext = await _dbEnumerator.MoveNextAsync().ConfigureAwait(false);
            }
            if (!_isMoveNext)
                return false;

            var entity = (T)await CreateEntity(_dbEnumerator, _dbEnumerator.Current, _dbEnumerator.Current, typeof(T)).ConfigureAwait(false);
            Object rawValue = _dbEnumerator.RawValue;

            _isMoveNext = await _dbEnumerator.MoveNextAsync().ConfigureAwait(false);
            if (!_isMoveNext && _queryContext != null && _queryContext.EntryFactory.SkipTokenAccessors.Length > 0)
                SetOrderByProperties(_queryContext, entity, rawValue);

            _current = entity;
            return true;
        }
        private static async Task SetNavigationProperty(IOeDbEnumerator dbEnumerator, Object value, Object entity)
        {
            PropertyInfo propertyInfo = entity.GetType().GetProperty(dbEnumerator.EntryFactory.EdmNavigationProperty.Name);
            Type nestedEntityType = OeExpressionHelper.GetCollectionItemType(propertyInfo.PropertyType);
            if (nestedEntityType == null)
                nestedEntityType = propertyInfo.PropertyType;

            Object navigationValue = await CreateNestedEntity(dbEnumerator, value, nestedEntityType).ConfigureAwait(false);
            propertyInfo.SetValue(entity, navigationValue);
        }
        private static void SetOrderByProperties(OeQueryContext queryContext, Object entity, Object value)
        {
            int i = 0;
            OrderByClause orderByClause = queryContext.ODataUri.OrderBy;
            while (orderByClause != null)
            {
                var propertyAccessNode = (SingleValuePropertyAccessNode)orderByClause.Expression;
                var properties = new List<IEdmProperty>() { propertyAccessNode.Property };
                if (propertyAccessNode.Source is SingleNavigationNode navigationNode)
                    do
                    {
                        properties.Add(navigationNode.NavigationProperty);
                    }
                    while ((navigationNode = navigationNode.Source as SingleNavigationNode) != null);

                Object orderValue = queryContext.EntryFactory.SkipTokenAccessors[i++].GetValue(value);
                SetPropertyValue(entity, properties, orderValue);

                orderByClause = orderByClause.ThenBy;
            }
        }
        private static void SetPropertyValue(Object entity, List<IEdmProperty> edmProperties, Object orderValue)
        {
            PropertyInfo clrProperty;
            for (int i = edmProperties.Count - 1; i > 0; i--)
            {
                clrProperty = entity.GetType().GetPropertyIgnoreCase(edmProperties[i]);
                Object navigationValue = clrProperty.GetValue(entity);
                if (navigationValue == null)
                {
                    navigationValue = Activator.CreateInstance(clrProperty.PropertyType);
                    clrProperty.SetValue(entity, navigationValue);
                }
                entity = navigationValue;
            }

            clrProperty = entity.GetType().GetPropertyIgnoreCase(edmProperties[0]);
            clrProperty.SetValue(entity, orderValue);
        }

        public override Object Current => _current;
        T IAsyncEnumerator<T>.Current => _current;
    }
}
