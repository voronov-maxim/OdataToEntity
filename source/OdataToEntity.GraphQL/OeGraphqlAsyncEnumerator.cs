using Microsoft.OData.Edm;
using OdataToEntity.Parsers;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity.GraphQL
{
    public sealed class OeGraphqlAsyncEnumerator : IAsyncEnumerator<Dictionary<String, Object?>>
    {
        private static readonly Dictionary<String, Object?> NullCurrent = new Dictionary<String, Object?>();

        private readonly CancellationToken _cancellationToken;
        private readonly Db.OeDbEnumerator _dbEnumerator;
        private bool _isFirstMoveNext;
        private bool _isMoveNext;

        public OeGraphqlAsyncEnumerator(IAsyncEnumerator<Object> asyncEnumerator, OeEntryFactory entryFactory, CancellationToken cancellationToken)
        {
            _dbEnumerator = new Db.OeDbEnumerator(asyncEnumerator, entryFactory);
            _cancellationToken = cancellationToken;
            _isFirstMoveNext = true;
            Current = NullCurrent;
        }

        private static async Task<Dictionary<String, Object?>> CreateEntity(Db.OeDbEnumerator dbEnumerator, Object value, Object entity, CancellationToken cancellationToken)
        {
            if (OeExpressionHelper.IsTupleType(entity.GetType()))
                value = entity;

            Dictionary<String, Object?> dictionary = CreateEntity(entity, dbEnumerator.EntryFactory.Accessors);
            foreach (OeEntryFactory navigationLink in dbEnumerator.EntryFactory.NavigationLinks)
            {
                var childDbEnumerator = (Db.OeDbEnumerator)dbEnumerator.CreateChild(navigationLink, cancellationToken);
                await SetNavigationProperty(childDbEnumerator, value, dictionary, cancellationToken).ConfigureAwait(false);
            }

            return dictionary;
        }
        private static Dictionary<String, Object?> CreateEntity(Object value, OePropertyAccessor[] accessors)
        {
            var entity = new Dictionary<String, Object?>(accessors.Length);
            for (int i = 0; i < accessors.Length; i++)
            {
                OePropertyAccessor accessor = accessors[i];
                entity[accessor.EdmProperty.Name] = accessor.GetValue(value);
            }
            return entity;
        }
        private static async Task<Object?> CreateNestedEntity(Db.OeDbEnumerator dbEnumerator, Object value, CancellationToken cancellationToken)
        {
            Object entity = dbEnumerator.Current;
            if (entity == null)
                return null;

            if (dbEnumerator.EntryFactory.EdmNavigationProperty.Type.Definition is EdmCollectionType)
            {
                var entityList = new List<Object>();
                do
                {
                    Object item = dbEnumerator.Current;
                    if (item != null)
                        entityList.Add(await CreateEntity(dbEnumerator, item, item, cancellationToken).ConfigureAwait(false));
                }
                while (await dbEnumerator.MoveNextAsync().ConfigureAwait(false));
                return entityList;
            }

            return await CreateEntity(dbEnumerator, value, entity, cancellationToken).ConfigureAwait(false);
        }
        public async ValueTask DisposeAsync()
        {
            await _dbEnumerator.DisposeAsync();
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

            Dictionary<String, Object?> entity = await CreateEntity(_dbEnumerator, _dbEnumerator.Current, _dbEnumerator.Current, _cancellationToken).ConfigureAwait(false);
            _dbEnumerator.ClearBuffer();

            _isMoveNext = await _dbEnumerator.MoveNextAsync().ConfigureAwait(false);
            Current = entity;
            return true;
        }
        private static async Task SetNavigationProperty(Db.OeDbEnumerator dbEnumerator, Object value, Dictionary<String, Object?> entity, CancellationToken cancellationToken)
        {
            Object? navigationValue = await CreateNestedEntity(dbEnumerator, value, cancellationToken).ConfigureAwait(false);
            entity[dbEnumerator.EntryFactory.EdmNavigationProperty.Name] = navigationValue;
        }

        public Dictionary<String, Object?> Current { get; private set; }
    }
}
