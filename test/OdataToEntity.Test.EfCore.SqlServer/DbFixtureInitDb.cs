using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity.Test
{
    public abstract class DbFixtureInitDb : DbFixture, IDisposable
    {
        private bool _initialized;
        private int _queryCount;

        protected DbFixtureInitDb(bool allowCache, bool useRelationalNulls) : base(allowCache, useRelationalNulls)
        {
        }

        public async override Task Initalize()
        {
            if (_initialized)
                return;

            _initialized = true;
            var parser = new OeParser(new Uri("http://dummy/"), base.OeDataAdapter, base.EdmModel);
            await parser.ExecuteOperationAsync(base.ParseUri("ResetDb"), OeRequestHeaders.JsonDefault, null, new MemoryStream(), CancellationToken.None);
            await base.ExecuteBatchAsync("Add");
        }

        public override async Task Execute<T, TResult>(QueryParameters<T, TResult> parameters)
        {
            _queryCount++;
            Task t1 = base.Execute(parameters);
            Task t2 = base.Execute(parameters);
            await Task.WhenAll(t1, t2);
        }
        public override async Task Execute<T, TResult>(QueryParametersScalar<T, TResult> parameters)
        {
            _queryCount++;
            Task t1 = base.Execute(parameters);
            Task t2 = base.Execute(parameters);
            await Task.WhenAll(t1, t2);
        }

        public void Dispose()
        {
            if (base.OeDataAdapter.QueryCache.AllowCache)
                Xunit.Assert.InRange(_queryCount, _queryCount, base.OeDataAdapter.QueryCache.CacheCount);
        }
    }

    public abstract class ManyColumnsFixtureInitDb : DbFixture, IDisposable
    {
        private bool _initialized;
        private int _queryCount;

        protected ManyColumnsFixtureInitDb(bool allowCache, bool useRelationalNulls) : base(allowCache, useRelationalNulls)
        {
        }

        public async override Task Initalize()
        {
            if (_initialized)
                return;

            _initialized = true;
            var parser = new OeParser(new Uri("http://dummy/"), base.OeDataAdapter, base.EdmModel);
            await parser.ExecuteOperationAsync(base.ParseUri("ResetManyColumns"), OeRequestHeaders.JsonDefault, null, new MemoryStream(), CancellationToken.None);
            await base.ExecuteBatchAsync("ManyColumns");
        }

        public override async Task Execute<T, TResult>(QueryParameters<T, TResult> parameters)
        {
            _queryCount++;
            Task t1 = base.Execute(parameters);
            Task t2 = base.Execute(parameters);
            await Task.WhenAll(t1, t2);
        }
        public override async Task Execute<T, TResult>(QueryParametersScalar<T, TResult> parameters)
        {
            _queryCount++;
            Task t1 = base.Execute(parameters);
            Task t2 = base.Execute(parameters);
            await Task.WhenAll(t1, t2);
        }

        public void Dispose()
        {
            if (base.OeDataAdapter.QueryCache.AllowCache)
                Xunit.Assert.InRange(_queryCount, _queryCount, base.OeDataAdapter.QueryCache.CacheCount);
        }
    }
}
