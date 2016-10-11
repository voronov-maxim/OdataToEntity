using Microsoft.EntityFrameworkCore;
using OdataToEntity.Test.Model;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace OdataToEntity.Test
{
    public sealed class BatchTest
    {
        [Fact]
        public async Task Add()
        {
            var fixture = new DbFixture(true);
            await fixture.ExecuteBatchAsync("Add");
            using (var orderContext = fixture.CreateContext())
            {
                Assert.Equal(3, orderContext.Orders.Count());
                Assert.Equal(6, orderContext.OrderItems.Count());

                var order1 = orderContext.Orders.Include(t => t.Items).Single(t => t.Name == "Order 1");
                Assert.Equal(3, order1.Items.Count());

                var order2 = orderContext.Orders.Include(t => t.Items).Single(t => t.Name == "Order 2");
                Assert.Equal(2, order2.Items.Count());

                var order3 = orderContext.Orders.Include(t => t.Items).Single(t => t.Name == "Order unknown");
                Assert.Equal(1, order3.Items.Count());
            }
        }
        [Fact]
        public async Task Delete()
        {
            var fixture = new DbFixture();
            await fixture.ExecuteBatchAsync("Delete");
            using (var orderContext = fixture.CreateContext())
            {
                Assert.Equal(4, orderContext.Customers.Count());
                Assert.Equal(2, orderContext.Orders.Count());
                Assert.Equal(4, orderContext.OrderItems.Count());

                var order1 = orderContext.Orders.Include(t => t.Items).Single(t => t.Name == "Order 1");
                Assert.Equal("Product order 1 item 3", order1.Items.Single().Product);
            }
        }
        [Fact]
        public async Task Update()
        {
            var fixture = new DbFixture();
            await fixture.ExecuteBatchAsync("Update");
            using (var orderContext = fixture.CreateContext())
            {
                Assert.Equal(4, orderContext.Customers.Count());
                Assert.Equal(3, orderContext.Orders.Count());
                Assert.Equal(6, orderContext.OrderItems.Count());

                var order1 = orderContext.Orders.Include(t => t.Items).Single(t => t.Id == 1);
                Assert.Equal("New Order 1", order1.Name);
                Assert.Equal("New Product order 1 item 3", order1.Items.Single(t => t.Id == 3).Product);

                Assert.Equal(Sex.Female, orderContext.Customers.Single(c => c.Id == 1).Sex);
                Assert.Equal(null, orderContext.Customers.Single(c => c.Id == 2).Sex);
            }
        }
    }
}
