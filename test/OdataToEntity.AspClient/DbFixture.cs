using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using ODataClient.Default;
using OdataToEntity.Test.Model;
using OdataToEntityCore.AspClient;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;

namespace OdataToEntity.Test
{
    internal sealed class DbFixture
    {
        private delegate IList ExecuteQueryFunc<out T>(IQueryable query, Expression expression);

        private sealed class DbQueryVisitor<T> : ExpressionVisitor
        {
            private readonly IQueryable _query;
            private ConstantExpression _parameter;

            public DbQueryVisitor(IQueryable query)
            {
                _query = query;
            }

            protected override Expression VisitParameter(ParameterExpression node)
            {
                if (_parameter == null && node.Type == typeof(IQueryable<T>))
                {
                    _parameter = Expression.Constant(_query);
                    return _parameter;
                }
                return base.VisitParameter(node);
            }
        }

        private sealed class TestContractResolver : DefaultContractResolver
        {
            protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
            {
                return base.CreateProperties(type, memberSerialization).OrderBy(p => p.PropertyName).ToList();
            }
        }

        private readonly String _databaseName;

        public DbFixture(bool clear = false)
        {
            _databaseName = OrderContext.GenerateDatabaseName();

            var client = new HttpClient() { BaseAddress = CreateContainer().BaseUri };
            client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "db/reset")).GetAwaiter().GetResult();
            if (!clear)
            {
                using (var context = OrderContext.Create(_databaseName))
                    InitDb(context);

                client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "db/init")).GetAwaiter().GetResult();
            }
        }

        public static Container CreateContainer()
        {
            return new Container(new Uri("http://localhost:5000/api"));
        }
        private static ExecuteQueryFunc<Object> CreateDelegate(Type elementType, ExecuteQueryFunc<Object> execFunc)
        {
            MethodInfo execMethodInfo = execFunc.GetMethodInfo().GetGenericMethodDefinition().MakeGenericMethod(elementType);
            var execFuncType = execFunc.GetType().GetGenericTypeDefinition().MakeGenericType(elementType);
            return (ExecuteQueryFunc<Object>)execMethodInfo.CreateDelegate(execFuncType);
        }
        public Task Execute<T, TResult>(QueryParametersScalar<T, TResult> parameters)
        {
            IList fromOe = ExecuteOe<T, TResult>(parameters.Expression);

            return Task.CompletedTask;
        }
        public Task Execute<T, TResult>(QueryParameters<T, TResult> parameters)
        {
            IList fromOe = ExecuteOe<T, TResult>(parameters.Expression);
            IList fromDb = ExecuteDb<T, TResult>(parameters.Expression);

            var settings = new JsonSerializerSettings()
            {
                ContractResolver = new TestContractResolver(),
                NullValueHandling = NullValueHandling.Ignore,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            };
            String jsonOe = JsonConvert.SerializeObject(fromOe, settings);
            String jsonDb = JsonConvert.SerializeObject(fromDb, settings);
            Xunit.Assert.Equal(jsonDb, jsonOe);

            return Task.CompletedTask;
        }
        private IList ExecuteDb<T, TResult>(LambdaExpression lambda)
        {
            using (var context = OrderContext.Create(_databaseName))
            {
                IQueryable query = GetQuerableDb<T>(context);
                var visitor = new DbQueryVisitor<T>(query);
                Expression call = visitor.Visit(lambda.Body);

                Type elementType;
                ExecuteQueryFunc<Object> func;
                if (call.Type.GetTypeInfo().IsGenericType)
                {
                    elementType = call.Type.GetGenericArguments()[0];
                    func = ExecuteQuery<Object>;
                }
                else
                {
                    elementType = typeof(Object);
                    func = ExecuteQueryScalar<Object>;
                }

                IList fromDb = CreateDelegate(elementType, func)(query, call);
                TestHelper.SetNullCollection(fromDb);
                return fromDb;
            }
        }
        private IList ExecuteOe<T, TResult>(LambdaExpression lambda)
        {
            Container container = CreateContainer();
            IQueryable query = GetQuerableOe<T>(container);
            var visitor = new TypeMapperVisitor(query) { TypeMap = t => Type.GetType("ODataClient." + t.FullName) };
            var call = visitor.Visit(lambda.Body);

            Type elementType;
            ExecuteQueryFunc<Object> func;
            if (call.Type.GetTypeInfo().IsGenericType)
            {
                elementType = call.Type.GetGenericArguments()[0];
                func = ExecuteQuery<Object>;
            }
            else
            {
                elementType = typeof(Object);
                func = ExecuteQueryScalar<Object>;
            }

            IList fromOe = CreateDelegate(elementType, func)(query, call);

            TestHelper.SetNullCollection(fromOe);
            //TestHelper.NormalizeOe<TResult>(fromOe);
            //if (elementType == typeof(ODataClient.OdataToEntity.Test.Model.Order))
            //{
            //    if (!visitor.IncludeProperties.Any(p => p.Name == nameof(Order.Items)))
            //        foreach (ODataClient.OdataToEntity.Test.Model.Order result in fromOe)
            //            result.Items = null;

            //    if (visitor.IncludeProperties.Any(p => p.Name == nameof(Order.AltCustomer)))
            //        foreach (ODataClient.OdataToEntity.Test.Model.Order result in fromOe)
            //            if (result.AltCustomer != null)
            //            {
            //                result.AltCustomer.Orders = null;
            //                if (!fromOe.Cast<ODataClient.OdataToEntity.Test.Model.Order>().Any(o => o.AltCustomer == result.AltCustomer))
            //                    result.AltCustomer.AltOrders = null;
            //            }

            //    if (visitor.IncludeProperties.Any(p => p.Name == nameof(Order.Customer)))
            //        foreach (ODataClient.OdataToEntity.Test.Model.Order result in fromOe)
            //            if (result.Customer != null)
            //            {
            //                result.Customer.AltOrders = null;
            //                if (!fromOe.Cast<ODataClient.OdataToEntity.Test.Model.Order>().Any(o => o.Customer == result.Customer))
            //                    result.Customer.Orders = null;
            //            }
            //}

            if (elementType == typeof(ODataClient.OdataToEntity.Test.Model.Customer))
            {
                if (!visitor.IncludeProperties.Any(p => p.Name == nameof(Customer.AltOrders)))
                    foreach (ODataClient.OdataToEntity.Test.Model.Customer result in fromOe)
                        result.AltOrders = null;

                if (!visitor.IncludeProperties.Any(p => p.Name == nameof(Customer.Orders)))
                    foreach (ODataClient.OdataToEntity.Test.Model.Customer result in fromOe)
                        result.Orders = null;
            }

            return fromOe;
        }
        private static IList ExecuteQuery<T>(IQueryable query, Expression expression)
        {
            IQueryable<T> newQuery = query.Provider.CreateQuery<T>(expression);
            return newQuery.ToList();
        }
        private static IList ExecuteQueryScalar<T>(IQueryable query, Expression expression)
        {
            T value = query.Provider.Execute<T>(expression);
            return new T[] { value };
        }
        private IQueryable GetQuerableDb<T>(OrderContext context)
        {
            if (typeof(T) == typeof(Customer))
                return context.Customers;
            if (typeof(T) == typeof(OrderItem))
                return context.OrderItems;
            if (typeof(T) == typeof(Order))
                return context.Orders;

            throw new InvalidOperationException("unknown type " + typeof(T).Name);
        }
        private IQueryable GetQuerableOe<T>(Container container)
        {
            if (typeof(T) == typeof(Customer))
                return container.Customers;
            if (typeof(T) == typeof(OrderItem))
                return container.OrderItems;
            if (typeof(T) == typeof(Order))
                return container.Orders;

            throw new InvalidOperationException("unknown type " + typeof(T).Name);
        }
        private static void InitDb(OrderContext context)
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
                AltCustomerId = 3,
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

            context.Customers.Add(customer1);
            context.Customers.Add(customer2);
            context.Customers.Add(customer3);
            context.Customers.Add(customer4);
            context.Orders.Add(order1);
            context.Orders.Add(order2);
            context.Orders.Add(order3);
            context.OrderItems.Add(orderItem11);
            context.OrderItems.Add(orderItem12);
            context.OrderItems.Add(orderItem13);
            context.OrderItems.Add(orderItem21);
            context.OrderItems.Add(orderItem22);
            context.OrderItems.Add(orderItem31);

            context.SaveChanges();
        }
    }
}
