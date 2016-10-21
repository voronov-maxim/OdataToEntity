using System;
using Microsoft.AspNetCore.Mvc;
using OdataToEntity.Test;
using System.Threading.Tasks;
using OdataToEntity.Test.Model;

namespace OdataToEntityCore.Asp
{
    public sealed class DbController : Controller
    {
        private readonly OrderDataAdapter _dataAdapter;

        public DbController(OrderDataAdapter dataAdapter)
        {
            _dataAdapter = dataAdapter;
        }

        public async Task Init()
        {
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
                Date = DateTimeOffset.Parse("2016-07-04T19:10:10.8237573+03:00"),
                Id = 1,
                Name = "Order 1",
                CustomerId = 1,
                Status = OrderStatus.Processing
            };
            var order2 = new Order()
            {
                Date = DateTimeOffset.Parse("2016-07-04T19:10:11.0000000+03:00"),
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

            OrderContext dbContext = null;
            try
            {
                dbContext = (OrderContext)_dataAdapter.CreateDataContext();

                dbContext.Customers.Add(customer1);
                dbContext.Customers.Add(customer2);
                dbContext.Customers.Add(customer3);
                dbContext.Customers.Add(customer4);
                dbContext.Orders.Add(order1);
                dbContext.Orders.Add(order2);
                dbContext.Orders.Add(order3);
                dbContext.OrderItems.Add(orderItem11);
                dbContext.OrderItems.Add(orderItem12);
                dbContext.OrderItems.Add(orderItem13);
                dbContext.OrderItems.Add(orderItem21);
                dbContext.OrderItems.Add(orderItem22);
                dbContext.OrderItems.Add(orderItem31);

                await dbContext.SaveChangesAsync();
            }
            finally
            {
                if (dbContext != null)
                    _dataAdapter.CloseDataContext(dbContext);
            }
        }
        public void Reset()
        {
            _dataAdapter.ResetDatabase();
        }
    }
}
