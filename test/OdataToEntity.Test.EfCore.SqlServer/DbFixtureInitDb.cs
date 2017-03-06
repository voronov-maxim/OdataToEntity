using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OdataToEntity.Test.Model;
using System;

namespace OdataToEntity.Test
{
    public class DbFixtureInitDb : DbFixture, IDisposable
    {
        private bool _initialized;
        private int _queryCount;

        public DbFixtureInitDb()
        {
        }

        public override void Initalize()
        {
            if (_initialized)
                return;

            _initialized = true;
            using (var context = new OrderContext())
                context.Database.ExecuteSqlCommand("dbo.ResetDb");
            base.ExecuteBatchAsync("Add").GetAwaiter().GetResult();
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
                Xunit.Assert.Equal(_queryCount, base.OeDataAdapter.QueryCache.CacheCount);
        }
    }
}
