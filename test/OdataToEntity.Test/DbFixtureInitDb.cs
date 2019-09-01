using Microsoft.EntityFrameworkCore;
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
            Db.OeDataAdapter dataAdapter = base.OeEdmModel.GetDataAdapter(typeof(OrderContext));
            return (OrderContext)dataAdapter.CreateDataContext();
        }
        internal static IEdmModel CreateEdmModel()
        {
            IEdmModel orderEdmModel = new OrderDataAdapter().BuildEdmModel();
            IEdmModel order2EdmModel = new Order2DataAdapter().BuildEdmModel(orderEdmModel);
            EnsureCreated(order2EdmModel);
            ExecuteBatchAsync(order2EdmModel, "Add").GetAwaiter().GetResult();
            return order2EdmModel;
        }
        public override Task Initalize()
        {
            return Task.CompletedTask;
        }
        private static void EnsureCreated(IEdmModel edmModel)
        {
            Db.OeDataAdapter dataAdapter = edmModel.GetDataAdapter(edmModel.EntityContainer);
            var dbContext = (DbContext)dataAdapter.CreateDataContext();
            dbContext.Database.EnsureCreated();

            if (dataAdapter.EntitySetAdapters.Find(typeof(OrderItemsView)) != null)
            {
                dbContext.Database.ExecuteSqlRaw("drop table OrderItemsView");
                dbContext.Database.ExecuteSqlRaw(
                    @"create view OrderItemsView(Name, Product) as select o.Name, i.Product from Orders o inner join OrderItems i on o.Id = i.OrderId");
            }

            dataAdapter.CloseDataContext(dbContext);

            foreach (IEdmModel refModel in edmModel.ReferencedModels)
                if (refModel.EntityContainer != null && refModel is EdmModel)
                    EnsureCreated(refModel);
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
            Db.OeDataAdapter dataAdapter = base.OeEdmModel.GetDataAdapter(typeof(OrderContext));
            return (OrderContext)dataAdapter.CreateDataContext();
        }
        public override Task Initalize()
        {
            return Task.CompletedTask;
        }

        protected internal override bool IsSqlite => true;
    }
}