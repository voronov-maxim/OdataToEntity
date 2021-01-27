using OdataToEntity.Test.Model;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace OdataToEntity.Test
{
    public sealed class InMemoryBatchTest
    {
        [Fact]
        public async Task Add()
        {
            var fixture = new RDBNull_DbFixtureInitDb();
            await fixture.Initalize().ConfigureAwait(false);

            using (var orderContext = fixture.CreateContext<InMemory.InMemoryOrderContext>())
            {
                Assert.Equal(8, orderContext.Categories.Count());
                Assert.Equal(4, orderContext.Customers.Count());
                Assert.Equal(4, orderContext.Orders.Count());
                Assert.Equal(7, orderContext.OrderItems.Count());
                Assert.Equal(5, orderContext.ShippingAddresses.Count());
                Assert.Equal(5, orderContext.CustomerShippingAddress.Count());

                var category = orderContext.Categories.Single(t => t.Name == "jackets");
                Assert.Equal("clothes", orderContext.Categories.Single(t => t.Id == category.ParentId).Name);
                Assert.Equal(2, orderContext.Categories.AsQueryable().Where(t => t.ParentId == category.Id).Count());

                int id = orderContext.Orders.Single(t => t.Name == "Order 1").Id;
                Assert.Equal(3, orderContext.OrderItems.Count(i => i.OrderId == id));

                id = orderContext.Orders.Single(t => t.Name == "Order 2").Id;
                Assert.Equal(2, orderContext.OrderItems.Count(i => i.OrderId == id));

                id = orderContext.Orders.Single(t => t.Name == "Order unknown").Id;
                Assert.Equal(2, orderContext.OrderItems.Count(i => i.OrderId == id));
            }
        }
        [Fact]
        public async Task Delete()
        {
            var fixture = new RDBNull_DbFixtureInitDb();
            await fixture.Initalize().ConfigureAwait(false);

            await DbFixture.ExecuteBatchAsync(fixture.OeEdmModel, "Delete").ConfigureAwait(false);
            using (var orderContext = fixture.CreateContext<InMemory.InMemoryOrderContext>())
            {
                Assert.Equal(5, orderContext.Categories.Count());
                Assert.Equal(4, orderContext.Customers.Count());
                Assert.Equal(3, orderContext.Orders.Count());
                Assert.Equal(3, orderContext.OrderItems.Count());
                Assert.Equal(2, orderContext.ShippingAddresses.Count());
                Assert.Equal(2, orderContext.CustomerShippingAddress.Count());

                int id = orderContext.Orders.Single(t => t.Name == "Order 1").Id;
                Assert.Equal("Product order 1 item 3", orderContext.OrderItems.Single(i => i.OrderId == id).Product);
            }
        }
        [Fact]
        public async Task Update()
        {
            var fixture = new RDBNull_DbFixtureInitDb();
            await fixture.Initalize().ConfigureAwait(false);

            await DbFixture.ExecuteBatchAsync(fixture.OeEdmModel, "Update").ConfigureAwait(false);
            using (var orderContext = fixture.CreateContext<InMemory.InMemoryOrderContext>())
            {
                var category = orderContext.Categories.Single(t => t.Name == "sombrero jacket");
                Assert.Equal("jackets", orderContext.Categories.Single(t => t.Id == category.ParentId).Name);

                Assert.Equal(4, orderContext.Customers.Count());
                Assert.Equal(4, orderContext.Orders.Count());
                Assert.Equal(7, orderContext.OrderItems.Count());

                Assert.Equal("New Order 1", orderContext.Orders.Single(t => t.Id == 1).Name);
                Assert.Equal("New Product order 1 item 3", orderContext.OrderItems.Single(t => t.OrderId == 1 && t.Id == 3).Product);

                Assert.Equal(Sex.Female, orderContext.Customers.Single(c => c.Country == "RU" && c.Id == 1).Sex);
                Assert.Null(orderContext.Customers.Single(c => c.Country == "EN" && c.Id == 1).Sex);
            }
        }
    }
}
