using Microsoft.OData.Edm;
using OdataToEntity.ModelBuilder;
using OdataToEntity.Test.Model;
using System;
using System.Threading.Tasks;

namespace OdataToEntity.Test
{
    public abstract class DbFixtureInitDb : DbFixture
    {
        private readonly String _databaseName;
        private bool _initialized;

        protected DbFixtureInitDb(bool allowCache, bool _, ModelBoundTestKind modelBoundTestKind)
            : this(allowCache, modelBoundTestKind, OrderContext.GenerateDatabaseName())
        {
        }
        private DbFixtureInitDb(bool allowCache, ModelBoundTestKind modelBoundTestKind, String databaseName)
            : base(CreateEdmModel(allowCache, databaseName), modelBoundTestKind)
        {
            _databaseName = databaseName;
        }

        public override OrderContext CreateContext()
        {
            return new OrderContext(OrderContextOptions.Create(_databaseName));
        }
        internal static EdmModel CreateEdmModel(bool allowCache, String databaseName)
        {
            var dataAdapter = new OrderDataAdapter(allowCache, databaseName);
            OeEdmModelMetadataProvider metadataProvider = OrderDataAdapter.CreateMetadataProvider();
            return OrderContextOptions.BuildEdmModel(dataAdapter, metadataProvider);
        }
        public async override Task Initalize()
        {
            if (_initialized)
                return;

            _initialized = true;
            EnsureCreated(EdmModel);
            await base.ExecuteBatchAsync("Add");
        }
    }

    public abstract class ManyColumnsFixtureInitDb : DbFixture
    {
        private readonly String _databaseName;
        private bool _initialized;

        protected ManyColumnsFixtureInitDb(bool allowCache, bool _, ModelBoundTestKind modelBoundTestKind)
            : this(allowCache, modelBoundTestKind, OrderContext.GenerateDatabaseName())
        {
        }
        private ManyColumnsFixtureInitDb(bool allowCache, ModelBoundTestKind modelBoundTestKind, String databaseName)
            : base(DbFixtureInitDb.CreateEdmModel(allowCache, databaseName), modelBoundTestKind)
        {
            _databaseName = databaseName;
        }

        public override OrderContext CreateContext()
        {
            return new OrderContext(OrderContextOptions.Create(_databaseName));
        }
        public async override Task Initalize()
        {
            if (_initialized)
                return;

            _initialized = true;
            EnsureCreated(EdmModel);
            await base.ExecuteBatchAsync("ManyColumns");
        }
    }
}
