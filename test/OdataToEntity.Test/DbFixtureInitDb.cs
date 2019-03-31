using Microsoft.OData.Edm;
using System.Threading.Tasks;

namespace OdataToEntity.Test
{
    public class DbFixtureInitDb : DbFixture
    {
        private bool _initialized;

        protected DbFixtureInitDb(bool allowCache, bool useRelationalNulls, ModelBoundTestKind modelNoundTestKind)
            : base(allowCache, useRelationalNulls, modelNoundTestKind)
        {
        }

        public async override Task Initalize()
        {
            if (_initialized)
                return;

            _initialized = true;
            EnsureCreated(base.EdmModel);
            await base.ExecuteBatchAsync("Add");
        }
    }

    public class ManyColumnsFixtureInitDb : DbFixture
    {
        private bool _initialized;

        protected ManyColumnsFixtureInitDb(bool allowCache, bool useRelationalNulls, ModelBoundTestKind modelNoundTestKind)
            : base(allowCache, useRelationalNulls, modelNoundTestKind)
        {
        }

        private static EdmModel BuildEdmModel(Db.OeDataAdapter dataAdapter, ModelBuilder.OeEdmModelMetadataProvider metadataProvider)
        {
            return new ModelBuilder.OeEdmModelBuilder(dataAdapter, metadataProvider).BuildEdmModel();
        }
        public async override Task Initalize()
        {
            if (_initialized)
                return;

            _initialized = true;
            EnsureCreated(base.EdmModel);
            await base.ExecuteBatchAsync("ManyColumns");
        }
    }
}
