using OdataToEntity.Parsers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity.Db
{
    public sealed class OeEntityDbEnumerator : IOeDbEnumerator
    {
        private readonly IAsyncEnumerator<Object?> _asyncEnumerator;
        private readonly OeEntityDbEnumerator? _parentEnumerator;

        public OeEntityDbEnumerator(IAsyncEnumerator<Object?> asyncEnumerator, OeEntryFactory entryFactory)
        {
            _asyncEnumerator = asyncEnumerator;
            EntryFactory = entryFactory;
        }
        public OeEntityDbEnumerator(IAsyncEnumerator<Object?> asyncEnumerator, OeNavigationEntryFactory entryFactory, OeEntityDbEnumerator parentEnumerator)
            : this(asyncEnumerator, entryFactory)
        {
            _parentEnumerator = parentEnumerator;
        }

        public void ClearBuffer()
        {
        }
        public IOeDbEnumerator CreateChild(OeNavigationEntryFactory entryFactory, CancellationToken cancellationToken)
        {
            IAsyncEnumerable<Object?> asyncEnumerable;
            Object? navigationValue = entryFactory.GetValue(Current);
            if (navigationValue is IEnumerable enumerable)
                asyncEnumerable = Infrastructure.AsyncEnumeratorHelper.ToAsyncEnumerable(enumerable);
            else
                asyncEnumerable = Infrastructure.AsyncEnumeratorHelper.ToAsyncEnumerable(Task.FromResult(navigationValue));

            IAsyncEnumerator<Object?> asyncEnumerator = asyncEnumerable.GetAsyncEnumerator(cancellationToken);
            asyncEnumerator.MoveNextAsync().GetAwaiter().GetResult();
            return new OeEntityDbEnumerator(asyncEnumerator, entryFactory, this);
        }
        public ValueTask DisposeAsync()
        {
            return _asyncEnumerator.DisposeAsync();
        }
        public ValueTask<bool> MoveNextAsync()
        {
            return _asyncEnumerator.MoveNextAsync();
        }

        public Object? Current => _asyncEnumerator.Current;
        public OeEntryFactory EntryFactory { get; }
        IOeDbEnumerator? IOeDbEnumerator.ParentEnumerator => _parentEnumerator;
        public Object? RawValue => _asyncEnumerator.Current;
    }
}
