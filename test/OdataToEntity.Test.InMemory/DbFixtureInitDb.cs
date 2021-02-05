using Microsoft.EntityFrameworkCore;
using Microsoft.OData.Edm;
using OdataToEntity.Test.Model;
using System;
using System.Threading.Tasks;

namespace OdataToEntity.Test
{
    public abstract class DbFixtureInitDb : DbFixture
    {
        private bool _initialized;

        protected DbFixtureInitDb(Type _, bool __, ModelBoundTestKind modelBoundTestKind)
            : this(modelBoundTestKind)
        {
        }
        private DbFixtureInitDb(ModelBoundTestKind modelBoundTestKind)
            : base(CreateEdmModel(), modelBoundTestKind, false)
        {
        }

        public override OrderContext CreateContext()
        {
            throw new NotImplementedException();
        }
        public InMemory.InMemoryOrderContext CreateInMemoryContext()
        {
            Db.OeDataAdapter dataAdapter = base.OeEdmModel.GetDataAdapter(typeof(InMemory.InMemoryOrderContext));
            return (InMemory.InMemoryOrderContext)dataAdapter.CreateDataContext();
        }
        internal static IEdmModel CreateEdmModel()
        {
            IEdmModel orderEdmModel = new OrderDataAdapter().BuildEdmModel();
            IEdmModel order2EdmModel = new Order2DataAdapter().BuildEdmModel(orderEdmModel);
            ExecuteBatchAsync(order2EdmModel, "Add").GetAwaiter().GetResult();
            ExecuteBatchAsync(orderEdmModel, "ManyColumns").GetAwaiter().GetResult();
            return order2EdmModel;
        }
        public override async Task Execute<T, TResult>(QueryParameters<T, TResult> parameters)
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
            using (var dbContext = new OrderContext(OrderContextOptions.Create(false)))
                await dbContext.Database.ExecuteSqlRawAsync("dbo.Initialize");
        }

        protected internal override bool IsSqlite => true;
    }

    public abstract class ManyColumnsFixtureInitDb : DbFixture
    {
        private bool _initialized;

        protected ManyColumnsFixtureInitDb(Type _, bool __, ModelBoundTestKind modelBoundTestKind)
            : base(DbFixtureInitDb.CreateEdmModel(), modelBoundTestKind, false)
        {
        }

        public override OrderContext CreateContext()
        {
            throw new NotImplementedException();
        }
        public InMemory.InMemoryOrder2Context CreateInMemoryContext()
        {
            Db.OeDataAdapter dataAdapter = base.OeEdmModel.GetDataAdapter(typeof(InMemory.InMemoryOrder2Context));
            return (InMemory.InMemoryOrder2Context)dataAdapter.CreateDataContext();
        }
        public async override Task Initalize()
        {
            if (_initialized)
                return;

            _initialized = true;
            using (var dbContext = new OrderContext(OrderContextOptions.Create(false)))
                await dbContext.Database.ExecuteSqlRawAsync("dbo.InitializeManyColumns");
        }

        protected internal override bool IsSqlite => true;
    }
}