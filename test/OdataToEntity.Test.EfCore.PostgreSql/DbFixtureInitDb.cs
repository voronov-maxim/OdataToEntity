using Microsoft.OData.Edm;
using OdataToEntity.ModelBuilder;
using OdataToEntity.Test.Model;
using System;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity.Test
{
    public abstract class DbFixtureInitDb : DbFixture
    {
        private bool _initialized;
        private readonly bool _useRelationalNulls;

        protected DbFixtureInitDb(Type _, bool useRelationalNulls, ModelBoundTestKind modelBoundTestKind)
            : base(CreateOeEdmModel(useRelationalNulls), modelBoundTestKind, useRelationalNulls)
        {
            _useRelationalNulls = useRelationalNulls;
        }

        public override OrderContext CreateContext()
        {
            return new OrderContext(OrderContextOptions.Create(_useRelationalNulls));
        }
        internal static EdmModel CreateOeEdmModel(bool useRelationalNulls)
        {
            var dataAdapter = new OrderDataAdapter(true, useRelationalNulls);
            OeEdmModelMetadataProvider metadataProvider = OrderDataAdapter.CreateMetadataProvider();
            bool allowCache = TestHelper.GetQueryCache(dataAdapter).AllowCache;
            var order2DataAdapter = new Order2DataAdapter(allowCache, useRelationalNulls);
            var refModel = new OeEdmModelBuilder(dataAdapter, metadataProvider).BuildEdmModel();
            return order2DataAdapter.BuildEdmModel(refModel);
        }
        public override async Task Execute<T, TResult>(QueryParameters<T, TResult> parameters)
        {
            parameters.Expression = (Expression<Func<IQueryable<T>, IQueryable<TResult>>>)new EfCore.Postgresql.OeDateTimeOffsetMembersVisitor().Visit(parameters.Expression);
            Task t1 = base.Execute(parameters);
            //Task t2 = base.Execute(parameters);zzz
            await Task.WhenAll(t1, Task.CompletedTask).ConfigureAwait(false);
        }
        public override async Task Execute<T, TResult>(QueryParametersScalar<T, TResult> parameters)
        {
            Task t1 = base.Execute(parameters);
            Task t2 = base.Execute(parameters);
            await Task.WhenAll(t1, t2).ConfigureAwait(false);
        }
        public async override Task Initalize()
        {
            if (_initialized)
                return;

            _initialized = true;
            var parser = new OeParser(new Uri("http://dummy/"), base.OeEdmModel);
            await parser.ExecuteOperationAsync(base.ParseUri("ResetDb"), OeRequestHeaders.JsonDefault, null, new MemoryStream(), CancellationToken.None).ConfigureAwait(false);
            await DbFixture.ExecuteBatchAsync(base.OeEdmModel, "Add").ConfigureAwait(false);
        }
    }

    public abstract class ManyColumnsFixtureInitDb : DbFixture
    {
        private bool _initialized;
        private readonly bool _useRelationalNulls;

        protected ManyColumnsFixtureInitDb(Type _, bool useRelationalNulls, ModelBoundTestKind modelBoundTestKind)
            : base(DbFixtureInitDb.CreateOeEdmModel(useRelationalNulls), modelBoundTestKind, useRelationalNulls)
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
            //Task t2 = base.Execute(parameters);zzz
            await Task.WhenAll(t1, Task.CompletedTask).ConfigureAwait(false);
        }
        public override async Task Execute<T, TResult>(QueryParametersScalar<T, TResult> parameters)
        {
            Task t1 = base.Execute(parameters);
            Task t2 = base.Execute(parameters);
            await Task.WhenAll(t1, t2).ConfigureAwait(false);
        }
        public async override Task Initalize()
        {
            if (_initialized)
                return;

            _initialized = true;
            var parser = new OeParser(new Uri("http://dummy/"), base.OeEdmModel);
            await parser.ExecuteOperationAsync(base.ParseUri("ResetManyColumns"), OeRequestHeaders.JsonDefault, null, new MemoryStream(), CancellationToken.None).ConfigureAwait(false);
            await DbFixture.ExecuteBatchAsync(base.OeEdmModel, "ManyColumns").ConfigureAwait(false);
        }
    }
}
