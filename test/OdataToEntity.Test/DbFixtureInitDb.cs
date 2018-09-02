using System.Threading.Tasks;

namespace OdataToEntity.Test
{
    public class DbFixtureInitDb : DbFixture
    {
        private bool _initialized;

        protected DbFixtureInitDb(bool allowCache, bool useRelationalNulls) : base(allowCache, useRelationalNulls)
        {
        }

        public async override Task Initalize()
        {
            if (_initialized)
                return;

            var dbContext = (Model.OrderContext)base.DbDataAdapter.CreateDataContext();
            dbContext.Database.EnsureCreated();
            base.DbDataAdapter.CloseDataContext(dbContext);

            _initialized = true;
            await base.ExecuteBatchAsync("Add");
        }
    }

    public class ManyColumnsFixtureInitDb : DbFixture
    {
        private bool _initialized;

        protected ManyColumnsFixtureInitDb(bool allowCache, bool useRelationalNulls) : base(allowCache, useRelationalNulls)
        {
        }

        public async override Task Initalize()
        {
            if (_initialized)
                return;

            var dbContext = (Model.OrderContext)base.DbDataAdapter.CreateDataContext();
            dbContext.Database.EnsureCreated();
            base.DbDataAdapter.CloseDataContext(dbContext);

            _initialized = true;
            await base.ExecuteBatchAsync("ManyColumns");
        }
    }
}
