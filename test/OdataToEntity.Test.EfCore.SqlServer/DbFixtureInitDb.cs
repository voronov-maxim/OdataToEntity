using Microsoft.OData.Edm;
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
        private readonly bool _useRelationalNulls;

        protected DbFixtureInitDb(bool useRelationalNulls, ModelBoundTestKind modelBoundTestKind)
            : base(CreateEdmModel(useRelationalNulls), modelBoundTestKind)
        {
            _useRelationalNulls = useRelationalNulls;
        }

        public override OrderContext CreateContext()
        {
            return new OrderContext(OrderContextOptions.Create(_useRelationalNulls));
        }
        internal static EdmModel CreateEdmModel(bool useRelationalNulls)
        {
            var dataAdapter = new OrderDataAdapter(true, useRelationalNulls);
            OeEdmModelMetadataProvider metadataProvider = OrderDataAdapter.CreateMetadataProvider();
            return OrderContextOptions.BuildEdmModel(dataAdapter, metadataProvider);
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
            var parser = new OeParser(new Uri("http://dummy/"), base.EdmModel);
            await parser.ExecuteOperationAsync(base.ParseUri("ResetDb"), OeRequestHeaders.JsonDefault, null, new MemoryStream(), CancellationToken.None);
            await base.ExecuteBatchAsync("Add");
        }
    }

    public abstract class ManyColumnsFixtureInitDb : DbFixture
    {
        private bool _initialized;
        private readonly bool _useRelationalNulls;

        protected ManyColumnsFixtureInitDb(bool useRelationalNulls, ModelBoundTestKind modelBoundTestKind)
            : base(DbFixtureInitDb.CreateEdmModel(useRelationalNulls), modelBoundTestKind)
        {
            _useRelationalNulls = useRelationalNulls;
        }

        public override OrderContext CreateContext()
        {
            return new OrderContext(OrderContextOptions.Create(_useRelationalNulls));
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
            var parser = new OeParser(new Uri("http://dummy/"), base.EdmModel);
            await parser.ExecuteOperationAsync(base.ParseUri("ResetManyColumns"), OeRequestHeaders.JsonDefault, null, new MemoryStream(), CancellationToken.None);
            await base.ExecuteBatchAsync("ManyColumns");
        }
    }
}
