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

        protected DbFixtureInitDb(bool _, ModelBoundTestKind modelBoundTestKind)
            : this(modelBoundTestKind, OrderContext.GenerateDatabaseName())
        {
        }
        private DbFixtureInitDb(ModelBoundTestKind modelBoundTestKind, String databaseName)
            : base(CreateEdmModel(databaseName), modelBoundTestKind)
        {
            _databaseName = databaseName;
        }

        public override OrderContext CreateContext()
        {
            return new OrderContext(OrderContextOptions.Create(_databaseName));
        }
        internal static EdmModel CreateEdmModel(String databaseName)
        {
            var dataAdapter = new OrderDataAdapter(databaseName);
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

        protected internal override bool IsSqlite => true;
    }

    public abstract class ManyColumnsFixtureInitDb : DbFixture
    {
        private readonly String _databaseName;
        private bool _initialized;

        protected ManyColumnsFixtureInitDb(bool _, ModelBoundTestKind modelBoundTestKind)
            : this(modelBoundTestKind, OrderContext.GenerateDatabaseName())
        {
        }
        private ManyColumnsFixtureInitDb(ModelBoundTestKind modelBoundTestKind, String databaseName)
            : base(DbFixtureInitDb.CreateEdmModel(databaseName), modelBoundTestKind)
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

        protected internal override bool IsSqlite => true;
    }
}
