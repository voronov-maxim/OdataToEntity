using Microsoft.OData.Edm;
using OdataToEntity.Test.Model;
using System;
using System.Threading.Tasks;

namespace OdataToEntity.Test
{
    public abstract class DbFixtureInitDb : DbFixture
    {
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
        public override InMemoryOrderContext CreateContext<InMemoryOrderContext>()
        {
            Db.OeDataAdapter dataAdapter = base.OeEdmModel.GetDataAdapter(typeof(InMemory.InMemoryOrderContext));
            return (InMemoryOrderContext)dataAdapter.CreateDataContext();
        }
        internal static IEdmModel CreateEdmModel()
        {
            IEdmModel orderEdmModel = new OrderDataAdapter().BuildEdmModel();
            IEdmModel order2EdmModel = new Order2DataAdapter().BuildEdmModel(orderEdmModel);
            ExecuteBatchAsync(orderEdmModel, "ManyColumns").GetAwaiter().GetResult();
            ExecuteBatchAsync(order2EdmModel, "Add").GetAwaiter().GetResult();
            return order2EdmModel;
        }
        public override async Task Execute<T, TResult>(QueryParameters<T, TResult> parameters)
        {
            Task t1 = base.Execute(parameters);
            Task t2 = base.Execute(parameters);
            await Task.WhenAll(t1, t2).ConfigureAwait(false);
        }
        public override Task Initalize()
        {
            return Task.CompletedTask;
        }

        protected internal override bool IsSqlite => true;
    }

    public abstract class ManyColumnsFixtureInitDb : DbFixture
    {
        protected ManyColumnsFixtureInitDb(Type _, bool __, ModelBoundTestKind modelBoundTestKind)
            : base(DbFixtureInitDb.CreateEdmModel(), modelBoundTestKind, false)
        {
        }

        public override OrderContext CreateContext()
        {
            throw new NotImplementedException();
        }
        public override InMemoryOrder2Context CreateContext<InMemoryOrder2Context>()
        {
            Db.OeDataAdapter dataAdapter = base.OeEdmModel.GetDataAdapter(typeof(InMemory.InMemoryOrder2Context));
            return (InMemoryOrder2Context)dataAdapter.CreateDataContext();
        }
        public override Task Initalize()
        {
            return Task.CompletedTask;
        }

        protected internal override bool IsSqlite => true;
    }
}