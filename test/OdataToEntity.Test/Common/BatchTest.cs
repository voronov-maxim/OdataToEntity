using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using OdataToEntity.Test.Model;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace OdataToEntity.Test
{
    public sealed class BatchTest
    {
        public void Action()
        {
            var fixture = new DbFixtureInitDb();
            var parser = new OeParser(new Uri("http://dummy/"), fixture.OeDataAdapter, fixture.EdmModel);
            var responseStream = new System.IO.MemoryStream();

            String data = JsonConvert.SerializeObject(new { id = 1, name = "Order 1", status = "Unknown" });
            System.IO.MemoryStream requestStream = null;//new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(data));

            //parser.ExecutePostAsync(new Uri(@"http://dummy/GetOrders(name='Order 1',id=1,status=null)"), requestStream, responseStream, System.Threading.CancellationToken.None).Wait();
            parser.ExecutePostAsync(new Uri(@"http://dummy/ResetDb"), requestStream, responseStream, System.Threading.CancellationToken.None).Wait();
        }

        [Fact]
        public Task Add()
        {
            var fixture = new DbFixtureInitDb();
            fixture.Initalize();

            using (var orderContext = fixture.CreateContext())
            {
                Assert.Equal(8, orderContext.Categories.Count());
                Assert.Equal(4, orderContext.Customers.Count());
                Assert.Equal(3, orderContext.Orders.Count());
                Assert.Equal(7, orderContext.OrderItems.Count());

                var category = orderContext.Categories.Single(t => t.Name == "jackets");
                Assert.Equal("clothes", orderContext.Categories.Single(t => t.Id == category.ParentId).Name);
                Assert.Equal(2, orderContext.Categories.Where(t => t.ParentId == category.Id).Count());

                var order1 = orderContext.Orders.Include(t => t.Items).Single(t => t.Name == "Order 1");
                Assert.Equal(3, order1.Items.Count());

                var order2 = orderContext.Orders.Include(t => t.Items).Single(t => t.Name == "Order 2");
                Assert.Equal(2, order2.Items.Count());

                var order3 = orderContext.Orders.Include(t => t.Items).Single(t => t.Name == "Order unknown");
                Assert.Equal(2, order3.Items.Count());
            }
            return Task.CompletedTask;
        }
        [Fact]
        public async Task Delete()
        {
            var fixture = new DbFixtureInitDb();
            fixture.Initalize();

            await fixture.ExecuteBatchAsync("Delete");
            using (var orderContext = fixture.CreateContext())
            {
                Assert.Equal(5, orderContext.Categories.Count());
                Assert.Equal(4, orderContext.Customers.Count());
                Assert.Equal(2, orderContext.Orders.Count());
                Assert.Equal(3, orderContext.OrderItems.Count());

                var order1 = orderContext.Orders.Include(t => t.Items).Single(t => t.Name == "Order 1");
                Assert.Equal("Product order 1 item 3", order1.Items.Single().Product);
            }
        }
        [Fact]
        public async Task Update()
        {
            var fixture = new DbFixtureInitDb();
            fixture.Initalize();

            await fixture.ExecuteBatchAsync("Update");
            using (var orderContext = fixture.CreateContext())
            {
                var category = orderContext.Categories.Single(t => t.Name == "sombrero jacket");
                Assert.Equal("jackets", orderContext.Categories.Single(t => t.Id == category.ParentId).Name);

                Assert.Equal(4, orderContext.Customers.Count());
                Assert.Equal(3, orderContext.Orders.Count());
                Assert.Equal(7, orderContext.OrderItems.Count());

                var order1 = orderContext.Orders.Include(t => t.Items).Single(t => t.Id == 1);
                Assert.Equal("New Order 1", order1.Name);
                Assert.Equal("New Product order 1 item 3", order1.Items.Single(t => t.Id == 3).Product);

                Assert.Equal(Sex.Female, orderContext.Customers.Single(c => c.Id == 1).Sex);
                Assert.Equal(null, orderContext.Customers.Single(c => c.Id == 2).Sex);
            }
        }
    }
}
