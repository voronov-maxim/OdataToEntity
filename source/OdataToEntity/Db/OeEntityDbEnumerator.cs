using OdataToEntity.Parsers;
using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity.Db
{
    public sealed class OeEntityDbEnumerator : IOeDbEnumerator
    {
        private readonly OeAsyncEnumerator _asyncEnumerator;

        public OeEntityDbEnumerator(OeAsyncEnumerator asyncEnumerator, OeEntryFactory entryFactory)
        {
            _asyncEnumerator = asyncEnumerator;
            EntryFactory = entryFactory;
        }

        public Object ClearBuffer()
        {
            return _asyncEnumerator.Current;
        }
        public IOeDbEnumerator CreateChild(OeEntryFactory entryFactory)
        {
            OeAsyncEnumerator asyncEnumerator;
            Object navigationValue = entryFactory.GetValue(Current);
            if (navigationValue is IEnumerable enumerable)
                asyncEnumerator = OeAsyncEnumerator.Create(enumerable, CancellationToken.None);
            else
                asyncEnumerator = OeAsyncEnumerator.Create(navigationValue, CancellationToken.None);

            asyncEnumerator.MoveNextAsync().GetAwaiter().GetResult();
            return new OeEntityDbEnumerator(asyncEnumerator, entryFactory);
        }
        public Task<bool> MoveNextAsync()
        {
            return _asyncEnumerator.MoveNextAsync();
        }

        public Object Current => _asyncEnumerator.Current;
        public OeEntryFactory EntryFactory { get; }
    }
}
