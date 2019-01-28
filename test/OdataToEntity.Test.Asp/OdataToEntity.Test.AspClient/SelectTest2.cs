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
                expectedResult = dbContext.OrderItems.Where(i => i.Order.Name == "Order 1" || i.Order.Name == "Order 2").Select(i => i.Id).ToList();

            String request = $"Customers/BoundFunctionCollection(orderNames=['Order 1','Order 2'])?$expand=Customer,Items&$select=Name";

            var queryParameters = new QueryParameters<Customer, Order>() { RequestUri = request };
            IList fromOe = await Fixture.ExecuteOeViaHttpClient(queryParameters, null);

            Assert.Equal(expectedResult, fromOe.Cast<Order>().SelectMany(c => c.Items).Select(i => i.Id).OrderBy(id => id));
        }
        [Fact]
        public async Task BoundFunctionSingle()
        {
            List<int> expectedResult;
            using (var dbContext = new OrderContext(OrderContextOptions.Create(true, null)))
                expectedResult = dbContext.OrderItems.Where(i =>
                    (i.Order.Name == "Order 1" || i.Order.Name == "Order 2") && i.Order.Customer.Country == "RU" && i.Order.Customer.Id == 1)
                    .Select(i => i.Id).ToList();

            String request = $"Customers('RU',1)/OdataToEntity.Test.Model.BoundFunctionSingle(orderNames=['Order 1','Order 2'])?$expand=Customer,Items&$select=Name";

            var queryParameters = new QueryParameters<Customer, Order>() { RequestUri = request };
            IList fromOe = await Fixture.ExecuteOeViaHttpClient(queryParameters, null);

            Assert.Equal(expectedResult, fromOe.Cast<Order>().SelectMany(c => c.Items).Select(i => i.Id).OrderBy(id => id));
        }
        [Fact]
        public async Task CountQueryParameter()
        {
            int expectedCount;
            using (var dbContext = new OrderContext(OrderContextOptions.Create(true, null)))
                expectedCount = dbContext.Orders.Count();

            String request = "Orders?&$count=true&$top=1";

            var queryParameters = new QueryParameters<Order>() { RequestUri = request };
            IList fromOe = await Fixture.ExecuteOeViaHttpClient(queryParameters, expectedCount);

            Assert.Equal(1, fromOe.Count);
        }
    }
}
