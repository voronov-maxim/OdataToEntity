using Microsoft.OData;
using Microsoft.OData.Edm;
using OdataToEntity.EfCore.DynamicDataContext;
using OdataToEntity.ModelBuilder;
using OdataToEntity.Test.Model;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity.Test
{
    public abstract class DbFixtureInitDb : DbFixture
    {
        private bool _initialized;

        protected DbFixtureInitDb(bool useRelationalNulls, ModelBoundTestKind modelBoundTestKind)
            : base(CreateEdmModel(useRelationalNulls), modelBoundTestKind, useRelationalNulls)
        {
        }

        public override OrderContext CreateContext()
        {
            throw new NotImplementedException();
        }
        internal static EdmModel CreateEdmModel(bool useRelationalNulls)
        {
            var dataAdapter = new DynamicDataAdapter(CreateTypeDefinitionManager(useRelationalNulls));
            return dataAdapter.BuildEdmModel();
        }
        private static DynamicTypeDefinitionManager CreateTypeDefinitionManager(bool useRelationalNulls)
        {
            IEdmModel edmModel = new OeEdmModelBuilder(new OrderDataAdapter(), new OeEdmModelMetadataProvider()).BuildEdmModel();
            var metadataProvider = new EdmDynamicMetadataProvider(edmModel);
            return DynamicTypeDefinitionManager.Create(DynamicDbContext.CreateOptions(useRelationalNulls), metadataProvider);
        }
        public override async Task Execute<T, TResult>(QueryParameters<T, TResult> parameters)
        {
            Task t1 = base.Execute(parameters);
            //Task t2 = base.Execute(parameters);zzz
            await Task.WhenAll(t1, Task.CompletedTask);
        }
        public override async Task Execute<T, TResult>(QueryParametersScalar<T, TResult> parameters)
        {
            Task t1 = base.Execute(parameters);
            //Task t2 = base.Execute(parameters);zzz
            await Task.WhenAll(t1, Task.CompletedTask);
        }
        public async override Task Initalize()
        {
            if (_initialized)
                return;

            _initialized = true;
            var parser = new OeParser(new Uri("http://dummy/"), base.DbEdmModel);
            ODataUri odataUri = OeParser.ParseUri(base.DbEdmModel, new Uri("ResetDb", UriKind.Relative));
            await parser.ExecuteOperationAsync(odataUri, OeRequestHeaders.JsonDefault, null, new MemoryStream(), CancellationToken.None);
            await ExecuteBatchAsync(base.OeEdmModel, "Add");
        }
    }

    public abstract class ManyColumnsFixtureInitDb : DbFixture
    {
        private bool _initialized;

        protected ManyColumnsFixtureInitDb(bool useRelationalNulls, ModelBoundTestKind modelBoundTestKind)
            : base(DbFixtureInitDb.CreateEdmModel(useRelationalNulls), modelBoundTestKind, useRelationalNulls)
        {
        }

        public override OrderContext CreateContext()
        {
            throw new NotImplementedException();
        }
        public override async Task Execute<T, TResult>(QueryParameters<T, TResult> parameters)
        {
            Task t1 = base.Execute(parameters);
            Task t2 = base.Execute(parameters);
            await Task.WhenAll(t1, t2);
        }
        public override async Task Execute<T, TResult>(QueryParametersScalar<T, TResult> parameters)
        {
            Task t1 = base.Execute(parameters);
            Task t2 = base.Execute(parameters);
            await Task.WhenAll(t1, t2);
        }
        public async override Task Initalize()
        {
            if (_initialized)
                return;

            _initialized = true;
            var parser = new OeParser(new Uri("http://dummy/"), base.DbEdmModel);
            ODataUri odataUri = OeParser.ParseUri(base.DbEdmModel, new Uri("ResetManyColumns", UriKind.Relative));
            await parser.ExecuteOperationAsync(odataUri, OeRequestHeaders.JsonDefault, null, new MemoryStream(), CancellationToken.None);
            await DbFixture.ExecuteBatchAsync(base.OeEdmModel, "ManyColumns");
        }
    }
}
