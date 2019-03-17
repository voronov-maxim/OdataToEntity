using OdataToEntity.Test;
using System;

namespace ODataClient.OdataToEntity.Test.Model
{
    public partial class Order
    {
        public Order()
        {
            CustomerCountry = OpenTypeConverter.NotSetString;
            CustomerId = Int32.MinValue;
            Date = DateTimeOffset.MinValue;
            Id = Int32.MinValue;
            Status = (OrderStatus)Int32.MinValue;
        }
    }

    public abstract partial class OrderBase
    {
        public OrderBase()
        {
            AltCustomerCountry = OpenTypeConverter.NotSetString;
            AltCustomerId = Int32.MinValue;
            Name = OpenTypeConverter.NotSetString;
        }
    }

    public partial class OrderItem
    {
        public OrderItem()
        {
            Count = Int32.MinValue;
            Id = Int32.MinValue;
            OrderId = Int32.MinValue;
            Price = Decimal.MinValue;
            Product = OpenTypeConverter.NotSetString;
        }
    }
}
