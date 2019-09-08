using Microsoft.OData.Edm;

namespace OdataToEntity.Test.WcfService
{
    public sealed class OrderServiceBehaviorAttribute : OdataWcfServiceBehaviorAttribute
    {
        public OrderServiceBehaviorAttribute() : base(typeof(Model.OrderDataAdapter))
        {
        }

        protected override OdataWcfService CreateOdataWcfService(Db.OeDataAdapter dataAdapter, IEdmModel edmModel)
        {
            return new OrderService(dataAdapter, edmModel);
        }
    }

    [OrderServiceBehavior]
    public sealed class OrderService : OdataWcfService
    {
        public OrderService(Db.OeDataAdapter dataAdapter, IEdmModel edmModel) : base(dataAdapter, edmModel)
        {
        }
    }
}
