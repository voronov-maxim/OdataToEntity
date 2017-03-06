using Microsoft.OData.Client;
using ODataClient.Default;
using ODataClient.OdataToEntity.Test.Model;
using OdataToEntity.Test;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace OdataToEntityCore.AspClient
{
    internal sealed class BatchTest
    {
        [Fact]
        public Task Add()
        {
            var fixture = new DbFixtureInitDb(true);
            fixture.Initalize();

            Container container = DbFixtureInitDb.CreateContainer();
            Add(container);
            container.SaveChanges(SaveChangesOptions.BatchWithSingleChangeset);

            container = DbFixtureInitDb.CreateContainer();

            Assert.Equal(8, container.Categories.Count());
            Assert.Equal(4, container.Customers.Count());
            Assert.Equal(3, container.Orders.Count());
            Assert.Equal(7, container.OrderItems.Count());

            var category = container.Categories.Where(t => t.Name == "jackets").Single();
            Assert.Equal("clothes", container.Categories.Where(t => t.Id == category.ParentId).Single().Name);
            Assert.Equal(2, container.Categories.Where(t => t.ParentId == category.Id).Count());

            var order1 = container.Orders.Expand(t => t.Items).Where(t => t.Name == "Order 1").Single();
            Assert.Equal(3, order1.Items.Count());

            var order2 = container.Orders.Expand(t => t.Items).Where(t => t.Name == "Order 2").Single();
            Assert.Equal(2, order2.Items.Count());

            var order3 = container.Orders.Expand(t => t.Items).Where(t => t.Name == "Order unknown").Single();
            Assert.Equal(2, order3.Items.Count());

            return Task.CompletedTask;
        }
        private static void Add(Container container)
        {
            var category1 = new Category()
            {
                Id = 1,
                Name = "clothes",
                ParentId = null
            };
            var category2 = new Category()
            {
                Id = 2,
                Name = "unknown",
                ParentId = null
            };
            var category3 = new Category()
            {
                Id = 3,
                Name = "hats",
                ParentId = 1
            };
            var category4 = new Category()
            {
                Id = 4,
                Name = "jackets",
                ParentId = 1
            };
            var category5 = new Category()
            {
                Id = 5,
                Name = "baseball cap",
                ParentId = 3
            };
            var category6 = new Category()
            {
                Id = 6,
                Name = "sombrero",
                ParentId = 3
            };
            var category7 = new Category()
            {
                Id = 7,
                Name = "fur coat",
                ParentId = 4
            };
            var category8 = new Category()
            {
                Id = 8,
                Name = "cloak",
                ParentId = 4
            };

            var customer1 = new Customer()
            {
                Address = "Moscow",
                Id = 1,
                Name = "Ivan",
                Sex = Sex.Male
            };
            var customer2 = new Customer()
            {
                Address = "Tambov",
                Id = 2,
                Name = "Natasha",
                Sex = Sex.Female
            };
            var customer3 = new Customer()
            {
                Address = "Tula",
                Id = 3,
                Name = "Sasha",
                Sex = Sex.Female
            };
            var customer4 = new Customer()
            {
                Address = null,
                Id = 4,
                Name = "Unknown",
                Sex = null
            };

            var order1 = new Order()
            {
                Date = DateTimeOffset.Now,
                Id = 1,
                Name = "Order 1",
                CustomerId = 1,
                Status = OrderStatus.Processing
            };
            var order2 = new Order()
            {
                Date = DateTimeOffset.Now,
                Id = 2,
                Name = "Order 2",
                CustomerId = 2,
                Status = OrderStatus.Processing
            };
            var order3 = new Order()
            {
                Date = null,
                Id = 3,
                Name = "Order unknown",
                CustomerId = 4,
                Status = OrderStatus.Unknown
            };

            var orderItem11 = new OrderItem()
            {
                Count = 1,
                Id = 1,
                OrderId = 1,
                Price = 1.1m,
                Product = "Product order 1 item 1"
            };
            var orderItem12 = new OrderItem()
            {
                Count = 2,
                Id = 2,
                OrderId = 1,
                Price = 1.2m,
                Product = "Product order 1 item 2"
            };
            var orderItem13 = new OrderItem()
            {
                Count = 3,
                Id = 3,
                OrderId = 1,
                Price = 1.3m,
                Product = "Product order 1 item 3"
            };

            var orderItem21 = new OrderItem()
            {
                Count = 1,
                Id = 4,
                OrderId = 2,
                Price = 2.1m,
                Product = "Product order 2 item 1"
            };
            var orderItem22 = new OrderItem()
            {
                Count = 2,
                Id = 5,
                OrderId = 2,
                Price = 2.2m,
                Product = "Product order 2 item 2"
            };
            var orderItem31 = new OrderItem()
            {
                Count = null,
                Id = 6,
                OrderId = 3,
                Price = null,
                Product = "Product order 3 item 1 (unknown)"
            };
            var orderItem32 = new OrderItem()
            {
                Count = 0,
                Id = 7,
                OrderId = 3,
                Price = 0,
                Product = "{ null }.Sum() == 0"
            };

            container.AddToCategories(category1);
            container.AddToCategories(category2);
            container.AddToCategories(category3);
            container.AddToCategories(category4);
            container.AddToCategories(category5);
            container.AddToCategories(category6);
            container.AddToCategories(category7);
            container.AddToCategories(category8);

            container.AddToCustomers(customer1);
            container.AddToCustomers(customer2);
            container.AddToCustomers(customer3);
            container.AddToCustomers(customer4);
            container.AddToOrders(order1);
            container.AddToOrders(order2);
            container.AddToOrders(order3);
            container.AddToOrderItems(orderItem11);
            container.AddToOrderItems(orderItem12);
            container.AddToOrderItems(orderItem13);
            container.AddToOrderItems(orderItem21);
            container.AddToOrderItems(orderItem22);
            container.AddToOrderItems(orderItem31);
            container.AddToOrderItems(orderItem32);
        }
        [Fact]
        public Task Delete()
        {
            var fixture = new DbFixtureInitDb();
            fixture.Initalize();

            Container container = DbFixtureInitDb.CreateContainer();
            Delete(container);
            container.SaveChanges(SaveChangesOptions.BatchWithSingleChangeset);

            container = DbFixtureInitDb.CreateContainer();

            Assert.Equal(5, container.Categories.Count());
            Assert.Equal(4, container.Customers.Count());
            Assert.Equal(2, container.Orders.Count());
            Assert.Equal(5, container.OrderItems.Count());

            var order1 = container.Orders.Expand(t => t.Items).Where(t => t.Name == "Order 1").Single();
            Assert.Equal("Product order 1 item 3", order1.Items.Single().Product);

            return Task.CompletedTask;
        }
        private static void Delete(Container container)
        {
            var category4 = new Category() { Id = 4 };
            container.AttachTo("Categories", category4);
            container.DeleteObject(category4);

            var category7 = new Category() { Id = 7 };
            container.AttachTo("Categories", category7);
            container.DeleteObject(category7);

            var category8 = new Category() { Id = 8 };
            container.AttachTo("Categories", category8);
            container.DeleteObject(category8);

            var orderItem1 = new OrderItem() { Id = 1 };
            container.AttachTo("OrderItems", orderItem1);
            container.DeleteObject(orderItem1);

            var orderItem2 = new OrderItem() { Id = 2 };
            container.AttachTo("OrderItems", orderItem2);
            container.DeleteObject(orderItem2);

            var order2 = new Order() { Id = 2 };
            container.AttachTo("Orders", order2);
            container.DeleteObject(order2);
        }
        [Fact]
        public Task Update()
        {
            var fixture = new DbFixtureInitDb();
            fixture.Initalize();

            Container container = DbFixtureInitDb.CreateContainer();
            Update(container);
            container.SaveChanges(SaveChangesOptions.BatchWithSingleChangeset);

            container = DbFixtureInitDb.CreateContainer();

            var category = container.Categories.Where(t => t.Name == "sombrero jacket").Single();
            Assert.Equal("jackets", container.Categories.Where(t => t.Id == category.ParentId).Single().Name);

            Assert.Equal(4, container.Customers.Count());
            Assert.Equal(3, container.Orders.Count());
            Assert.Equal(7, container.OrderItems.Count());

            var order1 = container.Orders.ByKey(1).Expand(t => t.Items).GetValue();
            Assert.Equal("New Order 1", order1.Name);
            Assert.Equal("New Product order 1 item 3", order1.Items.Single(t => t.Id == 3).Product);

            Assert.Equal(Sex.Female, container.Customers.ByKey(1).GetValue().Sex);
            Assert.Equal(null, container.Customers.ByKey(2).GetValue().Sex);

            return Task.CompletedTask;
        }
        private static void Update(Container container)
        {
            var category6 = container.Categories.ByKey(6).GetValue();
            category6.ParentId = 4;
            category6.Name = "sombrero jacket";
            container.ChangeState(category6, EntityStates.Modified);

            var order1 = container.Orders.ByKey(1).GetValue();
            order1.Name = "New " + order1.Name;
            container.ChangeState(order1, EntityStates.Modified);

            var orderItem13 = container.OrderItems.ByKey(3).GetValue();
            orderItem13.Product = "New " + orderItem13.Product;
            container.ChangeState(orderItem13, EntityStates.Modified);

            var customer1 = container.Customers.ByKey(1).GetValue();
            customer1.Sex = Sex.Female;
            container.ChangeState(customer1, EntityStates.Modified);

            var customer2 = container.Customers.ByKey(2).GetValue();
            customer2.Sex = null;
            container.ChangeState(customer2, EntityStates.Modified);
        }
    }
}
