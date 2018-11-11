using Microsoft.OData.Client;
using ODataClient.Default;
using ODataClient.OdataToEntity.Test.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace OdataToEntity.Test.AspClient
{
    internal static class DataServiceQueryExtension
    {
        public async static Task<T[]> ToArrayAsync<T>(this IEnumerable<T> source)
        {
            return (await ((DataServiceQuery<T>)source).ExecuteAsync()).ToArray();
        }
        public async static Task<T> SingleAsync<T>(this IEnumerable<T> source)
        {
            return (await ((DataServiceQuery<T>)source).ExecuteAsync()).Single();
        }
        public async static Task<List<T>> ToListAsync<T>(this IEnumerable<T> source)
        {
            return (await ((DataServiceQuery<T>)source).ExecuteAsync()).ToList();
        }
    }

    internal sealed class BatchTest
    {
        [Fact]
        public async Task Add()
        {
            var fixture = new DbFixtureInitDb(true);
            await fixture.Initalize();

            Container container = DbFixtureInitDb.CreateContainer(0);
            Add(container);
            await container.SaveChangesAsync(SaveChangesOptions.BatchWithSingleChangeset);

            container = DbFixtureInitDb.CreateContainer(0);

            Assert.Equal(8, (await container.Categories.ToListAsync()).Count);
            Assert.Equal(4, (await container.Customers.ToListAsync()).Count);
            Assert.Equal(3, (await container.Orders.ToListAsync()).Count);
            Assert.Equal(7, (await container.OrderItems.ToListAsync()).Count);
            Assert.Equal(5, (await container.ShippingAddresses.ToListAsync()).Count);
            Assert.Equal(5, (await container.CustomerShippingAddress.ToListAsync()).Count);

            var category = await container.Categories.Where(t => t.Name == "jackets").SingleAsync();
            Assert.Equal("clothes", (await container.Categories.Where(t => t.Id == category.ParentId).SingleAsync()).Name);
            Assert.Equal(2, (await container.Categories.Where(t => t.ParentId == category.Id).ToListAsync()).Count);

            var order1 = await container.Orders.Expand(t => t.Items).Where(t => t.Name == "Order 1").SingleAsync();
            Assert.Equal(3, order1.Items.Count());

            var order2 = await container.Orders.Expand(t => t.Items).Where(t => t.Name == "Order 2").SingleAsync();
            Assert.Equal(2, order2.Items.Count());

            var order3 = await container.Orders.Expand(t => t.Items).Where(t => t.Name == "Order unknown").SingleAsync();
            Assert.Equal(2, order3.Items.Count());
        }
        internal static void Add(Container container)
        {
            var category1 = new Category()
            {
                Id = -1,
                Name = "clothes",
                ParentId = null
            };
            var category2 = new Category()
            {
                Id = -2,
                Name = "unknown",
                ParentId = null
            };
            var category3 = new Category()
            {
                Id = -3,
                Name = "hats",
                ParentId = -1
            };
            var category4 = new Category()
            {
                Id = -4,
                Name = "jackets",
                ParentId = -1
            };
            var category5 = new Category()
            {
                Id = -5,
                Name = "baseball cap",
                ParentId = -3
            };
            var category6 = new Category()
            {
                Id = -6,
                Name = "sombrero",
                ParentId = -3
            };
            var category7 = new Category()
            {
                Id = -7,
                Name = "fur coat",
                ParentId = -4
            };
            var category8 = new Category()
            {
                Id = -8,
                Name = "cloak",
                ParentId = -4
            };

            var customer1 = new Customer()
            {
                Address = "Moscow",
                Country = "RU",
                Id = 1,
                Name = "Ivan",
                Sex = Sex.Male
            };
            var customer2 = new Customer()
            {
                Address = "London",
                Country = "EN",
                Id = 1,
                Name = "Natasha",
                Sex = Sex.Female
            };
            var customer3 = new Customer()
            {
                Address = "Tula",
                Country = "RU",
                Id = 2,
                Name = "Sasha",
                Sex = Sex.Female
            };
            var customer4 = new Customer()
            {
                Address = null,
                Country = "UN",
                Id = 1,
                Name = "Unknown",
                Sex = null
            };

            var order1 = new Order()
            {
                Date = DateTimeOffset.Now,
                Id = -1,
                Name = "Order 1",
                CustomerCountry = "RU",
                CustomerId = 1,
                Status = OrderStatus.Processing
            };
            var order2 = new Order()
            {
                Date = DateTimeOffset.Now,
                Id = -2,
                Name = "Order 2",
                CustomerCountry = "EN",
                CustomerId = 1,
                Status = OrderStatus.Processing
            };
            var order3 = new Order()
            {
                AltCustomerCountry = "RU",
                AltCustomerId = 2,
                Date = null,
                Id = -3,
                Name = "Order unknown",
                CustomerCountry = "UN",
                CustomerId = 1,
                Status = OrderStatus.Unknown
            };

            var orderItem11 = new OrderItem()
            {
                Count = 1,
                Id = -1,
                OrderId = -1,
                Price = 1.1m,
                Product = "Product order 1 item 1"
            };
            var orderItem12 = new OrderItem()
            {
                Count = 2,
                Id = -2,
                OrderId = -1,
                Price = 1.2m,
                Product = "Product order 1 item 2"
            };
            var orderItem13 = new OrderItem()
            {
                Count = 3,
                Id = -3,
                OrderId = -1,
                Price = 1.3m,
                Product = "Product order 1 item 3"
            };

            var orderItem21 = new OrderItem()
            {
                Count = 1,
                Id = -4,
                OrderId = -2,
                Price = 2.1m,
                Product = "Product order 2 item 1"
            };
            var orderItem22 = new OrderItem()
            {
                Count = 2,
                Id = -5,
                OrderId = -2,
                Price = 2.2m,
                Product = "Product order 2 item 2"
            };
            var orderItem31 = new OrderItem()
            {
                Count = null,
                Id = -6,
                OrderId = -3,
                Price = null,
                Product = "Product order 3 item 1 (unknown)"
            };
            var orderItem32 = new OrderItem()
            {
                Count = 0,
                Id = -7,
                OrderId = -3,
                Price = 0,
                Product = "{ null }.Sum() == 0"
            };

            var shippingAddress11 = new ShippingAddress()
            {
                Address = "Moscow 1",
                Id = 1,
                OrderId = -1
            };
            var shippingAddress12 = new ShippingAddress()
            {
                Address = "Moscow 2",
                Id = 2,
                OrderId = -1
            };
            var shippingAddress21 = new ShippingAddress()
            {
                Address = "London 1",
                Id = 1,
                OrderId = -2
            };
            var shippingAddress22 = new ShippingAddress()
            {
                Address = "London 2",
                Id = 2,
                OrderId = -2
            };
            var shippingAddress23 = new ShippingAddress()
            {
                Address = "London 3",
                Id = 3,
                OrderId = -2
            };

            var customerShippingAddress1 = new CustomerShippingAddress()
            {
                CustomerCountry = "EN",
                CustomerId = 1,
                ShippingAddressOrderId = -2,
                ShippingAddressId = 1
            };
            var customerShippingAddress2 = new CustomerShippingAddress()
            {
                CustomerCountry = "EN",
                CustomerId = 1,
                ShippingAddressOrderId = -2,
                ShippingAddressId = 2
            };
            var customerShippingAddress3 = new CustomerShippingAddress()
            {
                CustomerCountry = "EN",
                CustomerId = 1,
                ShippingAddressOrderId = -2,
                ShippingAddressId = 3
            };
            var customerShippingAddress4 = new CustomerShippingAddress()
            {
                CustomerCountry = "RU",
                CustomerId = 1,
                ShippingAddressOrderId = -1,
                ShippingAddressId = 1
            };
            var customerShippingAddress5 = new CustomerShippingAddress()
            {
                CustomerCountry = "RU",
                CustomerId = 1,
                ShippingAddressOrderId = -1,
                ShippingAddressId = 2
            };

            var manyColumns1 = new ManyColumns()
            {
                Column01 = 1,
                Column02 = 2,
                Column03 = 3,
                Column04 = 4,
                Column05 = 5,
                Column06 = 6,
                Column07 = 7,
                Column08 = 8,
                Column09 = 9,
                Column10 = 10,
                Column11 = 11,
                Column12 = 12,
                Column13 = 13,
                Column14 = 14,
                Column15 = 15,
                Column16 = 16,
                Column17 = 17,
                Column18 = 18,
                Column19 = 19,
                Column20 = 20,
                Column21 = 21,
                Column22 = 22,
                Column23 = 23,
                Column24 = 24,
                Column25 = 25,
                Column26 = 26,
                Column27 = 27,
                Column28 = 28,
                Column29 = 29,
                Column30 = 30
            };
            var manyColumns2 = new ManyColumns()
            {
                Column01 = 101,
                Column02 = 102,
                Column03 = 103,
                Column04 = 104,
                Column05 = 105,
                Column06 = 106,
                Column07 = 107,
                Column08 = 108,
                Column09 = 109,
                Column10 = 110,
                Column11 = 111,
                Column12 = 112,
                Column13 = 113,
                Column14 = 114,
                Column15 = 115,
                Column16 = 116,
                Column17 = 117,
                Column18 = 118,
                Column19 = 119,
                Column20 = 120,
                Column21 = 121,
                Column22 = 122,
                Column23 = 123,
                Column24 = 124,
                Column25 = 125,
                Column26 = 126,
                Column27 = 127,
                Column28 = 128,
                Column29 = 129,
                Column30 = 130
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

            container.AddToShippingAddresses(shippingAddress11);
            container.AddToShippingAddresses(shippingAddress12);
            container.AddToShippingAddresses(shippingAddress21);
            container.AddToShippingAddresses(shippingAddress22);
            container.AddToShippingAddresses(shippingAddress23);

            container.AddToCustomerShippingAddress(customerShippingAddress1);
            container.AddToCustomerShippingAddress(customerShippingAddress2);
            container.AddToCustomerShippingAddress(customerShippingAddress3);
            container.AddToCustomerShippingAddress(customerShippingAddress4);
            container.AddToCustomerShippingAddress(customerShippingAddress5);

            container.AddToManyColumns(manyColumns1);
            container.AddToManyColumns(manyColumns2);
        }
        [Fact]
        public async Task Delete()
        {
            var fixture = new DbFixtureInitDb();
            await fixture.Initalize();

            Container container = DbFixtureInitDb.CreateContainer(0);
            Delete(container);
            await container.SaveChangesAsync(SaveChangesOptions.BatchWithSingleChangeset);

            container = DbFixtureInitDb.CreateContainer(0);

            Assert.Equal(5, (await container.Categories.ToListAsync()).Count);
            Assert.Equal(4, (await container.Customers.ToListAsync()).Count);
            Assert.Equal(2, (await container.Orders.ToListAsync()).Count);
            Assert.Equal(3, (await container.OrderItems.ToListAsync()).Count);
            Assert.Equal(2, (await container.ShippingAddresses.ToListAsync()).Count);
            Assert.Equal(2, (await container.CustomerShippingAddress.ToListAsync()).Count);

            var order1 = await container.Orders.Expand(t => t.Items).Where(t => t.Name == "Order 1").SingleAsync();
            Assert.Equal("Product order 1 item 3", order1.Items.Single().Product);
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

            var customerShippingAddress1 = new CustomerShippingAddress() { CustomerCountry = "EN", CustomerId = 1, ShippingAddressOrderId = 2, ShippingAddressId = 1 };
            container.AttachTo("CustomerShippingAddress", customerShippingAddress1);
            container.DeleteObject(customerShippingAddress1);

            var customerShippingAddress2 = new CustomerShippingAddress() { CustomerCountry = "EN", CustomerId = 1, ShippingAddressOrderId = 2, ShippingAddressId = 2 };
            container.AttachTo("CustomerShippingAddress", customerShippingAddress2);
            container.DeleteObject(customerShippingAddress2);

            var customerShippingAddress3 = new CustomerShippingAddress() { CustomerCountry = "EN", CustomerId = 1, ShippingAddressOrderId = 2, ShippingAddressId = 3 };
            container.AttachTo("CustomerShippingAddress", customerShippingAddress3);
            container.DeleteObject(customerShippingAddress3);

            var orderItem1 = new OrderItem() { Id = 1 };
            container.AttachTo("OrderItems", orderItem1);
            container.DeleteObject(orderItem1);

            var orderItem2 = new OrderItem() { Id = 2 };
            container.AttachTo("OrderItems", orderItem2);
            container.DeleteObject(orderItem2);

            var orderItem4 = new OrderItem() { Id = 4 };
            container.AttachTo("OrderItems", orderItem4);
            container.DeleteObject(orderItem4);

            var orderItem5 = new OrderItem() { Id = 5 };
            container.AttachTo("OrderItems", orderItem5);
            container.DeleteObject(orderItem5);

            var shippingAddress21 = new ShippingAddress() { OrderId = 2, Id = 1 };
            container.AttachTo("ShippingAddresses", shippingAddress21);
            container.DeleteObject(shippingAddress21);

            var shippingAddress22 = new ShippingAddress() { OrderId = 2, Id = 2 };
            container.AttachTo("ShippingAddresses", shippingAddress22);
            container.DeleteObject(shippingAddress22);

            var shippingAddress23 = new ShippingAddress() { OrderId = 2, Id = 3 };
            container.AttachTo("ShippingAddresses", shippingAddress23);
            container.DeleteObject(shippingAddress23);

            var order2 = new Order() { Id = 2 };
            container.AttachTo("Orders", order2);
            container.DeleteObject(order2);
        }
        [Fact]
        public async Task Update()
        {
            var fixture = new DbFixtureInitDb();
            await fixture.Initalize();

            Container container = DbFixtureInitDb.CreateContainer(0);
            await Update(container);
            await container.SaveChangesAsync(SaveChangesOptions.BatchWithSingleChangeset);

            container = DbFixtureInitDb.CreateContainer(0);

            var category = await container.Categories.Where(t => t.Name == "sombrero jacket").SingleAsync();
            Assert.Equal("jackets", (await container.Categories.Where(t => t.Id == category.ParentId).SingleAsync()).Name);

            Assert.Equal(4, (await container.Customers.ToListAsync()).Count);
            Assert.Equal(3, (await container.Orders.ToListAsync()).Count);
            Assert.Equal(7, (await container.OrderItems.ToListAsync()).Count);

            var order1 = await container.Orders.ByKey(1).Expand(t => t.Items).GetValueAsync();
            Assert.Equal("New Order 1", order1.Name);
            Assert.Equal("New Product order 1 item 3", order1.Items.Single(t => t.Id == 3).Product);

            Assert.Equal(Sex.Female, (await container.Customers.ByKey("RU", 1).GetValueAsync()).Sex);
            Assert.Equal(null, (await container.Customers.ByKey("EN", 1).GetValueAsync()).Sex);
        }
        private async static Task Update(Container container)
        {
            var category6 = await container.Categories.ByKey(6).GetValueAsync();
            category6.ParentId = 4;
            category6.Name = "sombrero jacket";
            container.ChangeState(category6, EntityStates.Modified);

            var order1 = await container.Orders.ByKey(1).GetValueAsync();
            order1.Name = "New " + order1.Name;
            container.ChangeState(order1, EntityStates.Modified);

            var orderItem13 = await container.OrderItems.ByKey(3).GetValueAsync();
            orderItem13.Product = "New " + orderItem13.Product;
            container.ChangeState(orderItem13, EntityStates.Modified);

            var customer1 = await container.Customers.ByKey("RU", 1).GetValueAsync();
            customer1.Sex = Sex.Female;
            container.ChangeState(customer1, EntityStates.Modified);

            var customer2 = await container.Customers.ByKey("EN", 1).GetValueAsync();
            customer2.Sex = null;
            container.ChangeState(customer2, EntityStates.Modified);
        }
    }
}
