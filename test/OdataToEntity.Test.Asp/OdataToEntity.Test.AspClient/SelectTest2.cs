using OdataToEntity.Test.Model;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace OdataToEntity.Test
{
    partial class SelectTest
    {
        [Fact]
        public async Task BoundFunctionCollection()
        {
            List<int> expectedResult;
            using (var dbContext = new OrderContext(OrderContextOptions.Create(true, null)))
                expectedResult = dbContext.OrderItems.Where(i =>
                    i.Order.Customer.Name == "Natasha" ||
                    i.Order.Customer.Name == "Ivan" ||
                    i.Order.Customer.Name == "Sasha").Select(i => i.Id).ToList();

            String request = "Orders/OdataToEntity.Test.Model.BoundFunctionCollection(customerNames=['Natasha','Ivan','Sasha'])";

            var queryParameters = new QueryParameters<Order, OrderItem>() { RequestUri = request };
            IList fromOe = await Fixture.ExecuteOeViaHttpClient(queryParameters);

            Assert.Equal(expectedResult, fromOe.Cast<OrderItem>().Select(i => i.Id).OrderBy(id => id));
        }
        [Fact]
        public async Task BoundFunctionSingle()
        {
            int orderId;
            List<int> expectedResult;
            using (var dbContext = new OrderContext(OrderContextOptions.Create(true, null)))
            {
                orderId = dbContext.Orders.Single(i => i.Name == "Order 1").Id;
                expectedResult = dbContext.OrderItems.Where(i => i.OrderId == orderId &&
                    (i.Order.Customer.Name == "Natasha" ||
                    i.Order.Customer.Name == "Ivan" ||
                    i.Order.Customer.Name == "Sasha")).Select(i => i.Id).ToList();
            }

            String request = $"Orders({orderId})/OdataToEntity.Test.Model.BoundFunctionSingle(customerNames=['Natasha','Ivan','Sasha'])";

            var queryParameters = new QueryParameters<Order, OrderItem>() { RequestUri = request };
            IList fromOe = await Fixture.ExecuteOeViaHttpClient(queryParameters);

            Assert.Equal(expectedResult, fromOe.Cast<OrderItem>().Select(i => i.Id).OrderBy(id => id));
        }
    }
}
