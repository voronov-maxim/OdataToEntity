using Microsoft.EntityFrameworkCore;
using OdataToEntity.Test.Model;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace OdataToEntity.Test
{
    [AttributeUsageAttribute(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    internal sealed class NotPerformanceCahe : Attribute
    {
    }

    public abstract partial class SelectTest
    {
#if SQLITE
        public const string SkipTest = "Sqlite";
#else
        public const string SkipTest = null;
#endif
        protected SelectTest(DbFixtureInitDb fixture)
        {
            fixture.Initalize().GetAwaiter().GetResult();
            Fixture = fixture;
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task ApplyFilter(int pageSize)
        {
            var parameters = new QueryParameters<OrderItem>()
            {
                RequestUri = "OrderItems?$apply=filter(Order/Status eq OdataToEntity.Test.Model.OrderStatus'Processing')",
                Expression = t => t.Where(i => i.Order.Status == OrderStatus.Processing),
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task ApplyFilterGroupBy(int pageSize)
        {
            var parameters = new QueryParameters<Order, Object>()
            {
                //RequestUri = "Orders?$apply=filter(Status eq OdataToEntity.Test.Model.OrderStatus'Unknown')/groupby((Name), aggregate(Id with countdistinct as cnt))",
                RequestUri = "Orders?$apply=filter(Status eq OdataToEntity.Test.Model.OrderStatus'Unknown')/groupby((Name), aggregate(Id with sum as cnt))",
                //Expression = t => t.Where(o => o.Status == OrderStatus.Unknown).GroupBy(o => o.Name).Select(g => new { Name = g.Key, cnt = g.Select(o => o.Id).Distinct().Count() }),
                Expression = t => t.Where(o => o.Status == OrderStatus.Unknown).GroupBy(o => o.Name).Select(g => new { Name = g.Key, cnt = g.Sum(o => o.Id) }),
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task ApplyGroupBy(int pageSize)
        {
            var parameters = new QueryParameters<OrderItem, Object>()
            {
                RequestUri = "OrderItems?$apply=groupby((Product))&$orderby=Product",
                Expression = t => t.GroupBy(i => i.Product).Select(g => new { Product = g.Key }).OrderBy(a => a.Product),
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task ApplyGroupByAggregate(int pageSize)
        {
            var parameters = new QueryParameters<OrderItem, Object>()
            {
                //RequestUri = "OrderItems?$apply=groupby((OrderId, Order/Status), aggregate(Price with average as avg, Product with countdistinct as dcnt, Price with max as max, Order/Status with max as max_status, Price with min as min, Price with sum as sum, $count as cnt))&$orderby=OrderId",
                RequestUri = "OrderItems?$apply=groupby((OrderId, Order/Status), aggregate(Price with average as avg, length(Product) with sum as dcnt, Price with max as max, Order/Status with max as max_status, Price with min as min, Price with sum as sum, $count as cnt))&$orderby=OrderId",
                Expression = t => t.GroupBy(i => new { i.OrderId, i.Order.Status }).Select(g => new
                {
                    OrderId = g.Key.OrderId,
                    Order_Status = g.Key.Status,
                    avg = g.Average(i => i.Price),
                    //dcnt = g.Select(i => i.Product).Distinct().Count(),
                    dcnt = g.Sum(i => i.Product.Length),
                    max = g.Max(i => i.Price),
                    max_status = g.Max(_ => g.Key.Status),
                    min = g.Min(i => i.Price),
                    sum = g.Sum(i => i.Price),
                    cnt = g.Count()
                }).OrderBy(a => a.OrderId),
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory(Skip = SkipTest)]
        [InlineData(0)]
        [InlineData(1)]
        [NotPerformanceCahe]
        public async Task ApplyGropuByAggregateCompute(int pageSize)
        {
            var parameters = new QueryParameters<OrderItem, Object>()
            {
                RequestUri = "OrderItems?$apply=groupby((Order/Id, Order/Name),aggregate(Price with sum as sum))/compute(length(Order/Name) as nameLength)&$orderby=Order/Id",
                Expression = t => t.GroupBy(i => new { i.Order.Id, i.Order.Name }).Select(g => new
                {
                    Order_Id = g.Key.Id,
                    Order_Name = g.Key.Name,
                    sum = g.Sum(i => i.Price),
                    nameLength = g.Key.Name.Length
                }).OrderBy(g => g.Order_Id),
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory(Skip = SkipTest)]
        [InlineData(0)]
        [InlineData(1)]
        public async Task ApplyGroupByAggregateFilter(int pageSize)
        {
            var parameters = new QueryParameters<OrderItem, Object>()
            {
                RequestUri = "OrderItems?$apply=groupby((OrderId), aggregate(Price with sum as sum))/filter(OrderId eq 2 and sum ge 4)",
                Expression = t => t.GroupBy(i => i.OrderId).Select(g => new
                {
                    OrderId = g.Key,
                    sum = g.Sum(i => i.Price)
                }).Where(a => a.OrderId == 2 && a.sum >= 4),
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task ApplyGroupByAggregateFilterOrdinal(int pageSize)
        {
            var parameters = new QueryParameters<OrderItem, Object>()
            {
                RequestUri = "OrderItems?$apply=groupby((OrderId), aggregate(Price with sum as sum))&$filter=OrderId eq 2",
                Expression = t => t.GroupBy(i => i.OrderId).Select(g => new
                {
                    OrderId = g.Key,
                    sum = g.Sum(i => i.Price)
                }).Where(a => a.OrderId == 2),
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory(Skip = SkipTest)]
        [InlineData(0)]
        [InlineData(1)]
        public async Task ApplyGroupByAggregateOrderBy(int pageSize)
        {
            var parameters = new QueryParameters<OrderItem, Object>()
            {
                RequestUri = "OrderItems?$apply=groupby((OrderId), aggregate(Price with sum as sum))&$orderby=sum",
                Expression = t => t.GroupBy(i => i.OrderId).Select(g => new
                {
                    OrderId = g.Key,
                    sum = g.Sum(i => i.Price)
                }).OrderBy(a => a.sum),
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task ApplyGroupByFilter(int pageSize)
        {
            var parameters = new QueryParameters<OrderItem, Object>()
            {
                RequestUri = "OrderItems?$apply=groupby((OrderId, Order/Name))/filter(OrderId eq 1 and Order/Name eq 'Order 1')",
                Expression = t => t.GroupBy(i => new { i.OrderId, i.Order.Name })
                    .Where(g => g.Key.OrderId == 1 && g.Key.Name == "Order 1")
                    .Select(g => new { OrderId = g.Key.OrderId, Order_Name = g.Key.Name }),
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task ApplyGroupByMul(int pageSize)
        {
            var parameters = new QueryParameters<OrderItem, Object>()
            {
                RequestUri = "OrderItems?$apply=groupby((OrderId), aggregate(Price mul Count with sum as sum))&$orderby=OrderId",
                Expression = t => t.GroupBy(i => i.OrderId).Select(g => new { OrderId = g.Key, sum = g.Sum(i => i.Price * i.Count) }).OrderBy(a => a.OrderId),
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task ApplyGroupByOrderBy(int pageSize)
        {
            var parameters = new QueryParameters<OrderItem, Object>()
            {
                RequestUri = "OrderItems?$apply=groupby((OrderId, Order/Name))&$orderby=OrderId desc, Order/Name",
                Expression = t => t.GroupBy(i => new { i.OrderId, i.Order.Name })
                    .Select(g => new { OrderId = g.Key.OrderId, Order_Name = g.Key.Name })
                    .OrderByDescending(a => a.OrderId).ThenBy(a => a.Order_Name),
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Fact]
        public async Task ApplyGroupByOrderBySkipTop()
        {
            var parameters = new QueryParameters<OrderItem, Object>()
            {
                RequestUri = "OrderItems?$apply=groupby((OrderId, Order/Name))&$orderby=OrderId desc, Order/Name&$skip=1&$top=1",
                Expression = t => t.GroupBy(i => new { i.OrderId, i.Order.Name })
                    .Select(g => new { OrderId = g.Key.OrderId, Order_Name = g.Key.Name })
                    .OrderByDescending(a => a.OrderId).ThenBy(a => a.Order_Name).Skip(1).Take(1)
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task ApplyGroupBySkip(int pageSize)
        {
            var parameters = new QueryParameters<OrderItem, Object>()
            {
                RequestUri = "OrderItems?$apply=groupby((OrderId))&$orderby=OrderId&$skip=1",
                Expression = t => t.GroupBy(i => i.OrderId).OrderBy(g => g.Key).Skip(1).Select(g => new { OrderId = g.Key }),
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Fact]
        public async Task ApplyGroupByTop()
        {
            var parameters = new QueryParameters<OrderItem, Object>()
            {
                RequestUri = "OrderItems?$apply=groupby((OrderId))&$top=1&$orderby=OrderId",
                Expression = t => t.GroupBy(i => i.OrderId).OrderBy(g => g.Key).Take(1).Select(g => new { OrderId = g.Key })
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task ApplyGroupByVirtualCount(int pageSize)
        {
            var parameters = new QueryParameters<OrderItem, Object>()
            {
                //RequestUri = "OrderItems?$apply=groupby((OrderId), aggregate(substring(Product, 0, 10) with countdistinct as dcnt, $count as cnt))/filter(dcnt ne cnt)&$orderby=OrderId",
                RequestUri = "OrderItems?$apply=groupby((OrderId), aggregate(length(Product) with sum as dcnt, $count as cnt))/filter(dcnt ne cnt)&$orderby=OrderId",
                Expression = t => t.GroupBy(i => i.OrderId).Select(g => new
                {
                    OrderId = g.Key,
                    //dcnt = g.Select(i => i.Product.Substring(0, 10)).Distinct().Count(),
                    dcnt = g.Sum(i => i.Product.Length),
                    cnt = g.Count()
                }).Where(a => a.dcnt != a.cnt).OrderBy(a => a.OrderId),
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Compute(bool navigationNextLink)
        {
            var parameters = new QueryParameters<OrderItem, Object>()
            {
                RequestUri = "OrderItems?$select=Product&$expand=Order&$compute=Count mul Price as Total,Id add OrderId as SumId",
                Expression = t => t.Select(i => new
                {
                    Product = i.Product,
                    Order = i.Order,
                    Total = i.Count * i.Price,
                    SumId = i.Id + i.OrderId
                }),
                NavigationNextLink = navigationNextLink
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Fact]
        public async Task Count()
        {
            var parameters = new QueryParametersScalar<Order, int>()
            {
                RequestUri = "Orders/$count",
                Expression = t => t.Count(),
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task DbQuery(int pageSize)
        {
            var parameters = new QueryParameters<OrderItemsView>()
            {
                RequestUri = "OrderItemsView?$orderby=Name,Product",
                Expression = t => t.OrderBy(v => v.Name).ThenBy(v => v.Product),
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0, false)]
        [InlineData(1, false)]
        [InlineData(0, true)]
        [InlineData(1, true)]
        public async Task Expand(int pageSize, bool navigationNextLink)
        {
            var parameters = new QueryParameters<Order>()
            {
                RequestUri = "Orders?$expand=Customer,Items&$orderby=Id",
                Expression = t => t.Include(o => o.Customer).Include(o => o.Items).OrderBy(o => o.Id),
                NavigationNextLink = navigationNextLink,
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0, false)]
        [InlineData(1, false)]
        [InlineData(0, true)]
        [InlineData(1, true)]
        public async Task ExpandCollectionSingleSelectNestedName(int pageSize, bool navigationNextLink)
        {
            var parameters = new QueryParameters<Customer, Object>()
            {
                RequestUri = "Customers?$expand=CustomerShippingAddresses($select=ShippingAddress;$expand=ShippingAddress($select=Address))&$select=Name&$orderby=Country,Id",
                Expression = t => t.Include(c => c.CustomerShippingAddresses).ThenInclude(s => s.ShippingAddress)
                .OrderBy(c => c.Country).ThenBy(c => c.Id).Select(c => new
                {
                    CustomerShippingAddresses = c.CustomerShippingAddresses.Select(s => new
                    {
                        ShippingAddress = new
                        {
                            s.ShippingAddress.Address
                        }
                    }),
                    c.Name
                }),
                NavigationNextLink = navigationNextLink,
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0, false)]
        [InlineData(1, false)]
        [InlineData(0, true)]
        [InlineData(1, true)]
        public async Task ExpandManyToMany(int pageSize, bool navigationNextLink)
        {
            var parameters = new QueryParameters<Customer, Object>()
            {
                RequestUri = "Customers?$expand=AltOrders,ShippingAddresses,Orders&$orderby=Country,Id",
                Expression = t => t.Include(c => c.AltOrders).Include(c => c.ShippingAddresses).Include(c => c.Orders).Include(c => c.CustomerShippingAddresses)
                .ThenInclude(csa => csa.ShippingAddress)
                .OrderBy(c => c.Country).ThenBy(c => c.Id)
                .Select(c => new Customer()
                {
                    Address = c.Address,
                    AltOrders = c.AltOrders,
                    Country = c.Country,
                    Id = c.Id,
                    Name = c.Name,
                    Sex = c.Sex,
                    Orders = c.Orders,
                    ShippingAddresses = c.CustomerShippingAddresses.Select(csa => csa.ShippingAddress).ToList()
                }),
                NavigationNextLink = navigationNextLink,
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0, false)]
        [InlineData(1, false)]
        [InlineData(0, true)]
        [InlineData(1, true)]
        public async Task ExpandAndSelect(int pageSize, bool navigationNextLink)
        {
            var parameters = new QueryParameters<Order, Object>()
            {
                RequestUri = "Orders?$expand=AltCustomer,Customer,Items($orderby=Id),ShippingAddresses($orderby=Id)&$select=AltCustomerCountry,AltCustomerId,CustomerCountry,CustomerId,Date,Id,Name,Status&$orderby=Id",
                Expression = t => t.Select(o => new { o.AltCustomer, o.Customer, o.Items, o.AltCustomerCountry, o.AltCustomerId, o.CustomerCountry, o.CustomerId, o.Date, o.Id, o.Name, o.ShippingAddresses, o.Status }).OrderBy(o => o.Id),
                NavigationNextLink = navigationNextLink,
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0, false)]
        [InlineData(1, false)]
        [InlineData(0, true)]
        [InlineData(1, true)]
        public async Task ExpandExpandFilter(int pageSize, bool navigationNextLink)
        {
            var parameters = new QueryParameters<Customer, Customer>()
            {
                RequestUri = "Customers?$orderby=Country,Id&$expand=AltOrders($expand=Items($filter=contains(Product,'unknown');$orderby=Id)),Orders($expand=Items($filter=contains(Product,'unknown');$orderby=Id))",
                Expression = t => t.Include(c => c.AltOrders).ThenInclude(o => o.Items.Where(i => i.Product.Contains("unknown"))).Include(c => c.Orders).ThenInclude(o => o.Items.Where(i => i.Product.Contains("unknown"))).OrderBy(c => c.Country).ThenBy(c => c.Id),
                NavigationNextLink = navigationNextLink,
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0, false)]
        [InlineData(1, false)]
        [InlineData(0, true)]
        [InlineData(1, true)]
        public async Task ExpandExpandMany(int pageSize, bool navigationNextLink)
        {
            var parameters = new QueryParameters<Customer, Customer>()
            {
                RequestUri = "Customers?$expand=AltOrders($expand=Items($orderby=Price desc),ShippingAddresses($orderby=Id desc)),Orders($expand=Items($orderby=Price desc),ShippingAddresses($orderby=Id desc))&$orderby=Country,Id",
                Expression = t => t.Include(c => c.AltOrders).ThenInclude(o => o.Items.OrderByDescending(i => i.Price)).Include(c => c.Orders).ThenInclude(o => o.Items.OrderByDescending(i => i.Price)).Include(c => c.Orders).ThenInclude(o => o.ShippingAddresses.OrderByDescending(s => s.Id)).OrderBy(c => c.Country).ThenBy(c => c.Id),
                NavigationNextLink = navigationNextLink,
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0, false)]
        [InlineData(1, false)]
        [InlineData(0, true)]
        [InlineData(1, true)]
        public async Task ExpandExpandOne(int pageSize, bool navigationNextLink)
        {
            var parameters = new QueryParameters<OrderItem, OrderItem>()
            {
                RequestUri = "OrderItems?$expand=Order($expand=AltCustomer,Customer)&$orderby=Id",
                Expression = t => t.Include(i => i.Order).ThenInclude(o => o.AltCustomer).Include(i => i.Order).ThenInclude(o => o.Customer).OrderBy(i => i.Id),
                NavigationNextLink = navigationNextLink,
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0, false)]
        [InlineData(1, false)]
        [InlineData(0, true)]
        [InlineData(1, true)]
        public async Task ExpandExpandOrderBy(int pageSize, bool navigationNextLink)
        {
            var parameters = new QueryParameters<Customer, Customer>()
            {
                RequestUri = "Customers?$expand=Orders($expand=Items($orderby=Id desc))&$orderby=Country,Id",
                Expression = t => t.Include(c => c.Orders).ThenInclude(o => o.Items.OrderByDescending(i => i.Id)).OrderBy(c => c.Country).ThenBy(c => c.Id),
                NavigationNextLink = navigationNextLink,
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory(Skip = SkipTest)]
        [InlineData(0, false)]
        [InlineData(1, false)]
        [InlineData(0, true)]
        [InlineData(1, true)]
        public async Task ExpandExpandSkipTop(int pageSize, bool navigationNextLink)
        {
            var parameters = new QueryParameters<Customer, Customer>()
            {
                RequestUri = "Customers?$orderby=Country,Id&$skip=1&$top=3&$expand=AltOrders($expand=Items($orderby=Id;$top=1)),Orders($expand=Items($orderby=Id;$top=1))",
                Expression = t => t.OrderBy(c => c.Country).ThenBy(c => c.Id).Skip(1).Take(3).Include(c => c.AltOrders).ThenInclude(o => o.Items.Take(1)).Include(c => c.Orders).ThenInclude(o => o.Items.Take(1)),
                NavigationNextLink = navigationNextLink,
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0, false)]
        [InlineData(1, false)]
        [InlineData(0, true)]
        [InlineData(1, true)]
        public async Task ExpandInverseProperty(int pageSize, bool navigationNextLink)
        {
            var parameters = new QueryParameters<Customer, Customer>()
            {
                RequestUri = "Customers?$expand=AltOrders,Orders&$orderby=Country,Id",
                Expression = t => t.Include(c => c.AltOrders).Include(c => c.Orders).OrderBy(c => c.Country).ThenBy(c => c.Id),
                NavigationNextLink = navigationNextLink,
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0, false)]
        [InlineData(1, false)]
        [InlineData(0, true)]
        [InlineData(1, true)]
        public async Task ExpandManyFilter(int pageSize, bool navigationNextLink)
        {
            var parameters = new QueryParameters<Customer, Customer>()
            {
                RequestUri = "Customers?$expand=Orders($filter=Status eq OdataToEntity.Test.Model.OrderStatus'Processing')&$orderby=Country,Id",
                Expression = t => t.Include(c => c.Orders.Where(o => o.Status == OrderStatus.Processing)).OrderBy(c => c.Country).ThenBy(c => c.Id),
                NavigationNextLink = navigationNextLink,
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0, false)]
        [InlineData(1, false)]
        [InlineData(0, true)]
        [InlineData(1, true)]
        public async Task ExpandNestedSelect(int pageSize, bool navigationNextLink)
        {
            var parameters = new QueryParameters<Customer, Customer>()
            {
                RequestUri = "Customers?$expand=Orders($select=AltCustomerCountry,AltCustomerId,CustomerCountry,CustomerId,Date,Id,Name,Status)&$orderby=Country,Id",
                Expression = t => t.Include(c => c.Orders).OrderBy(c => c.Country).ThenBy(c => c.Id),
                NavigationNextLink = navigationNextLink,
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0, false)]
        [InlineData(1, false)]
        [InlineData(0, true)]
        [InlineData(1, true)]
        public async Task ExpandNullableNestedSelect(int pageSize, bool navigationNextLink)
        {
            var parameters = new QueryParameters<Order>()
            {
                RequestUri = "Orders?$expand=AltCustomer($select=Address,Country,Id,Name,Sex)&$orderby=Id",
                Expression = t => t.Include(o => o.AltCustomer).OrderBy(o => o.Id),
                NavigationNextLink = navigationNextLink,
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0, false)]
        [InlineData(1, false)]
        [InlineData(0, true)]
        [InlineData(1, true)]
        public async Task ExpandOneFilter(int pageSize, bool navigationNextLink)
        {
            IQueryable<Customer> customers = null;
            var parameters = new QueryParameters<Order>()
            {
                RequestUri = "Orders?$expand=AltCustomer($filter=Sex eq OdataToEntity.Test.Model.Sex'Male')",
                Expression = t => t.GroupJoin(customers.Where(c => c.Sex == Sex.Male),
                    o => new { Country = o.AltCustomerCountry, Id = o.AltCustomerId },
                    c => new { c.Country, Id = (int?)c.Id },
                    (o, c) => new { Order = o, Customer = c })
                    .SelectMany(z => z.Customer.DefaultIfEmpty(), (o, c) => new { o.Order, Customer = c }).Select(a =>
                    new Order()
                    {
                        AltCustomerCountry = a.Order.AltCustomerCountry,
                        AltCustomerId = a.Order.AltCustomerId,
                        Customer = a.Customer,
                        CustomerCountry = a.Order.CustomerCountry,
                        CustomerId = a.Order.CustomerId,
                        Date = a.Order.Date,
                        Id = a.Order.Id,
                        Name = a.Order.Name,
                        Status = a.Order.Status
                    }),
                NavigationNextLink = navigationNextLink,
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0, false)]
        [InlineData(1, false)]
        [InlineData(0, true)]
        [InlineData(1, true)]
        public async Task ExpandStar(int pageSize, bool navigationNextLink)
        {
            var parameters = new QueryParameters<Order>()
            {
                RequestUri = "Orders?$expand=*&$orderby=Id",
                Expression = t => t.Include(o => o.AltCustomer).Include(o => o.Customer).Include(o => o.Items).Include(o => o.ShippingAddresses).OrderBy(o => o.Id),
                NavigationNextLink = navigationNextLink,
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task FilterAll(int pageSize)
        {
            var parameters = new QueryParameters<Order>()
            {
                RequestUri = "Orders?$filter=Items/all(d:d/Price ge 2.1)",
                Expression = t => t.Where(o => o.Items.All(i => i.Price >= 2.1m)),
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task FilterAndHaving(int pageSize)
        {
            var parameters = new QueryParameters<OrderItem, Object>()
            {
                RequestUri = "OrderItems?$filter=Order/CustomerCountry ne 'UN'&$apply=groupby((OrderId), aggregate(Price mul Count with sum as sum))/filter(sum gt 7)",
                Expression = t => t.Where(i => i.Order.CustomerCountry != "UN").GroupBy(i => i.OrderId)
                    .Select(g => new { OrderId = g.Key, sum = g.Sum(i => i.Price * i.Count) }).Where(a => a.sum > 7),
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task FilterAny(int pageSize)
        {
            var parameters = new QueryParameters<Order>()
            {
                RequestUri = "Orders?$filter=Items/any(d:d/Count gt 2)",
                Expression = t => t.Where(o => o.Items.Any(i => i.Count > 2)),
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task FilterApplyGroupBy(int pageSize)
        {
            var parameters = new QueryParameters<Order, Object>()
            {
                //RequestUri = "Orders?$filter=Status eq OdataToEntity.Test.Model.OrderStatus'Unknown'&$apply=groupby((Name), aggregate(Id with countdistinct as cnt))",
                RequestUri = "Orders?$filter=Status eq OdataToEntity.Test.Model.OrderStatus'Unknown'&$apply=groupby((Name), aggregate(Id with sum as cnt))",
                Expression = t => t.Where(o => o.Status == OrderStatus.Unknown).GroupBy(o => o.Name).Select(g => new { Name = g.Key, cnt = g.Sum(o => o.Id) }),
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task FilterCount(int pageSize)
        {
            var parameters = new QueryParameters<Order>()
            {
                RequestUri = "Orders?$filter=Items/$count gt 2",
                Expression = t => t.Where(o => o.Items.Count() > 2),
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task FilterDateTime(int pageSize)
        {
            var dt = DateTime.Parse("2016-07-04T19:10:10.8237573+03:00", null, System.Globalization.DateTimeStyles.AdjustToUniversal);
            var parameters = new QueryParameters<Category>()
            {
                RequestUri = "Categories?$filter=DateTime ge 2016-07-04T19:10:10.8237573%2B03:00",
                Expression = t => t.Where(o => o.DateTime >= dt),
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task FilterDateTimeNull(int pageSize)
        {
            var parameters = new QueryParameters<Category>()
            {
                RequestUri = "Categories?$filter=DateTime eq null",
                Expression = t => t.Where(o => o.DateTime == null),
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task FilterDateTimeOffset(int pageSize)
        {
            var parameters = new QueryParameters<Order>()
            {
                RequestUri = "Orders?$filter=Date ge 2016-07-04T19:10:10.8237573%2B03:00",
                Expression = t => t.Where(o => o.Date >= DateTimeOffset.Parse("2016-07-04T19:10:10.8237573+03:00")),
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task FilterDateTimeOffsetNull(int pageSize)
        {
            var parameters = new QueryParameters<Order>()
            {
                RequestUri = "Orders?$filter=Date eq null",
                Expression = t => t.Where(o => o.Date == null),
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task FilterDateTimeOffsetYearMonthDay(int pageSize)
        {
            var parameters = new QueryParameters<Order>()
            {
                RequestUri = "Orders?$filter=year(Date) eq 2016 and month(Date) gt 3 and day(Date) lt 20",
                Expression = t => t.Where(o => o.Date.Value.Year == 2016 && o.Date.Value.Month > 3 && o.Date.Value.Day < 20),
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task FilterDateTimeYearMonthDay(int pageSize)
        {
            var parameters = new QueryParameters<Category>()
            {
                RequestUri = "Categories?$filter=year(DateTime) eq 2016 and month(DateTime) gt 3 and day(DateTime) lt 20",
                Expression = t => t.Where(c => c.DateTime.Value.Year == 2016 && c.DateTime.Value.Month > 3 && c.DateTime.Value.Day < 20),
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task FilterDecimal(int pageSize)
        {
            var parameters = new QueryParameters<OrderItem>()
            {
                RequestUri = "OrderItems?$filter=Price gt 2",
                Expression = t => t.Where(i => i.Price > 2),
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task FilterDecimalNull(int pageSize)
        {
            var parameters = new QueryParameters<OrderItem>()
            {
                RequestUri = "OrderItems?$filter=Price eq null",
                Expression = t => t.Where(i => i.Price == null),
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task FilterEnum(int pageSize)
        {
            var parameters = new QueryParameters<Customer>()
            {
                RequestUri = "Customers?$filter=Sex eq OdataToEntity.Test.Model.Sex'Female'",
                Expression = t => t.Where(c => c.Sex == Sex.Female),
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task FilterEnumGe(int pageSize)
        {
            var parameters = new QueryParameters<Order>()
            {
                RequestUri = "Orders?$filter=Status ge OdataToEntity.Test.Model.OrderStatus'Unknown'&$orderby=Id",
                Expression = t => t.Where(o => o.Status >= OrderStatus.Unknown).OrderBy(o => o.Id),
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task FilterEnumLt(int pageSize)
        {
            var parameters = new QueryParameters<Order>()
            {
                RequestUri = "Orders?$filter=OdataToEntity.Test.Model.OrderStatus'Unknown' lt Status",
                Expression = t => t.Where(o => OrderStatus.Unknown < o.Status),
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task FilterEnumNullableGe(int pageSize)
        {
            var parameters = new QueryParameters<Customer>()
            {
                RequestUri = "Customers?$filter=Sex ge OdataToEntity.Test.Model.Sex'Male'&$orderby=Country,Id",
                Expression = t => t.Where(c => c.Sex >= Sex.Male).OrderBy(c => c.Country).ThenBy(c => c.Id),
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task FilterEnumNullableLt(int pageSize)
        {
            var parameters = new QueryParameters<Customer>()
            {
                RequestUri = "Customers?$filter=OdataToEntity.Test.Model.Sex'Male' lt Sex&$orderby=Country,Id",
                Expression = t => t.Where(c => Sex.Male < c.Sex).OrderBy(c => c.Country).ThenBy(c => c.Id),
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task FilterEnumNull(int pageSize)
        {
            var parameters = new QueryParameters<Customer>()
            {
                RequestUri = "Customers?$filter=Sex eq null",
                Expression = t => t.Where(c => c.Sex == null),
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task FilterEnumNotNullAndStringNotNull(int pageSize)
        {
            var parameters = new QueryParameters<Customer>()
            {
                RequestUri = "Customers?$filter=Sex ne null and Address ne null&$orderby=Country,Id",
                Expression = t => t.Where(c => c.Sex != null && c.Address != null).OrderBy(c => c.Country).ThenBy(c => c.Id),
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task FilterEnumNullAndStringNotNull(int pageSize)
        {
            var parameters = new QueryParameters<Customer>()
            {
                RequestUri = "Customers?$filter=Sex eq null and Address ne null",
                Expression = t => t.Where(c => c.Sex == null && c.Address != null),
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task FilterEnumNullAndStringNull(int pageSize)
        {
            var parameters = new QueryParameters<Customer>()
            {
                RequestUri = "Customers?$filter=Sex eq null and Address eq null",
                Expression = t => t.Where(c => c.Sex == null && c.Address == null),
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task FilterIn(int pageSize)
        {
            var parameters = new QueryParameters<OrderItem>()
            {
                RequestUri = "OrderItems?$filter=Price in (1.1,1.2,1.3)&$orderby=Id",
                Expression = t => t.Where(i => i.Price == 1.1m || i.Price == 1.2m || i.Price == 1.3m).OrderBy(i => i.Id),
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task FilterInEnum(int pageSize)
        {
            var parameters = new QueryParameters<Customer>()
            {
                RequestUri = "Customers?$filter=Sex in ('Male','Female')&$orderby=Country,Id",
                Expression = t => t.Where(c => c.Sex == Sex.Male || c.Sex == Sex.Female).OrderBy(c => c.Country).ThenBy(c => c.Id),
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task FilterInt(int pageSize)
        {
            var parameters = new QueryParameters<OrderItem>()
            {
                RequestUri = "OrderItems?$filter=Count ge 2",
                Expression = t => t.Where(i => i.Count >= 2),
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task FilterIntNull(int pageSize)
        {
            var parameters = new QueryParameters<OrderItem>()
            {
                RequestUri = "OrderItems?$filter=Count eq null",
                Expression = t => t.Where(i => i.Count == null),
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task FilterNavigation(int pageSize)
        {
            var parameters = new QueryParameters<OrderItem>()
            {
                RequestUri = "OrderItems?$filter=Order/Customer/Name eq 'Ivan'",
                Expression = t => t.Where(i => i.Order.Customer.Name == "Ivan"),
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task FilterSegment(int pageSize)
        {
            var parameters = new QueryParameters<Customer>()
            {
                RequestUri = "Customers/$filter(Sex eq OdataToEntity.Test.Model.Sex'Female')?$orderby=Id",
                Expression = t => t.Where(c => c.Sex == Sex.Female).OrderBy(c => c.Id),
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task FilterStringConcat(int pageSize)
        {
            var parameters = new QueryParameters<Customer>()
            {
                RequestUri = "Customers?$filter=concat(concat(Name,' hello'),' world') eq 'Ivan hello world'",
                Expression = t => t.Where(c => (c.Name + " hello world") == "Ivan hello world"),
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task FilterStringContains(int pageSize)
        {
            var parameters = new QueryParameters<Customer>()
            {
                RequestUri = "Customers?$filter=contains(Name, 'sh')",
                Expression = t => t.Where(c => c.Name.Contains("sh")),
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task FilterStringEq(int pageSize)
        {
            var parameters = new QueryParameters<Customer>()
            {
                RequestUri = "Customers?$filter=Address eq 'Tula'",
                Expression = t => t.Where(c => c.Address == "Tula"),
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task FilterStringGe(int pageSize)
        {
            var parameters = new QueryParameters<Customer>()
            {
                RequestUri = "Customers?$filter=Address ge 'Tula'",
                Expression = t => t.Where(c => String.Compare(c.Address, "Tula") >= 0),
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task FilterStringGt(int pageSize)
        {
            var parameters = new QueryParameters<Customer>()
            {
                RequestUri = "Customers?$filter=Address gt 'London'&$orderby=Country,Id",
                Expression = t => t.Where(c => String.Compare(c.Address, "London") > 0).OrderBy(c => c.Country).ThenBy(c => c.Id),
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task FilterStringLe(int pageSize)
        {
            var parameters = new QueryParameters<Customer>()
            {
                RequestUri = "Customers?$filter=Address le 'Tula'&$orderby=Country,Id",
                Expression = t => t.Where(c => String.Compare(c.Address, "Tula") <= 0).OrderBy(c => c.Country).ThenBy(c => c.Id),
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task FilterStringLt(int pageSize)
        {
            var parameters = new QueryParameters<Customer>()
            {
                RequestUri = "Customers?$filter=Address lt 'Tula'&$orderby=Country,Id",
                Expression = t => t.Where(c => String.Compare(c.Address, "Tula") < 0).OrderBy(c => c.Country).ThenBy(c => c.Id),
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task FilterStringNe(int pageSize)
        {
            var parameters = new QueryParameters<Customer>()
            {
                RequestUri = "Customers?$filter=Address ne 'Tula'&$orderby=Country,Id",
                Expression = t => t.Where(c => c.Address != "Tula").OrderBy(c => c.Country).ThenBy(c => c.Id),
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task FilterStringEndsWith(int pageSize)
        {
            var parameters = new QueryParameters<Customer>()
            {
                RequestUri = "Customers?$filter=endswith(Name, 'asha')",
                Expression = t => t.Where(c => c.Name.EndsWith("asha")),
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task FilterStringLength(int pageSize)
        {
            var parameters = new QueryParameters<Customer>()
            {
                RequestUri = "Customers?$filter=length(Name) eq 5",
                Expression = t => t.Where(c => c.Name.Length == 5),
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task FilterStringIndexOf(int pageSize)
        {
            var parameters = new QueryParameters<Customer>()
            {
                RequestUri = "Customers?$filter=indexof(Name, 'asha') eq 1",
                Expression = t => t.Where(c => c.Name.IndexOf("asha") == 1),
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task FilterStringStartsWith(int pageSize)
        {
            var parameters = new QueryParameters<Customer>()
            {
                RequestUri = "Customers?$filter=startswith(Name, 'S')",
                Expression = t => t.Where(c => c.Name.StartsWith("S")),
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task FilterStringSubstring(int pageSize)
        {
            var parameters = new QueryParameters<Customer>()
            {
                RequestUri = "Customers?$filter=substring(Name, 1, 1) eq substring(Name, 4, 1)",
                Expression = t => t.Where(c => c.Name.Substring(1, 1) == c.Name.Substring(4, 1)),
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task FilterStringToLower(int pageSize)
        {
            var parameters = new QueryParameters<Customer>()
            {
                RequestUri = "Customers?$filter=tolower(Name) eq 'sasha'",
                Expression = t => t.Where(c => c.Name.ToLower() == "sasha"),
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task FilterStringToUpper(int pageSize)
        {
            var parameters = new QueryParameters<Customer>()
            {
                RequestUri = "Customers?$filter=toupper(Name) eq 'SASHA'",
                Expression = t => t.Where(c => c.Name.ToUpper() == "SASHA"),
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task FilterStringTrim(int pageSize)
        {
            var parameters = new QueryParameters<Customer>()
            {
                RequestUri = "Customers?$filter=trim(concat(Name, ' ')) eq trim(Name)&$orderby=Country,Id",
                Expression = t => t.Where(c => (c.Name + " ").Trim() == c.Name.Trim()).OrderBy(c => c.Country).ThenBy(c => c.Id),
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task KeyComposition(int pageSize)
        {
            var parameters = new QueryParameters<Customer>()
            {
                RequestUri = "Customers(Country='RU',Id=1)",
                Expression = t => t.Where(c => c.Country == "RU" && c.Id == 1),
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0, false)]
        [InlineData(1, false)]
        [InlineData(0, true)]
        [InlineData(1, true)]
        public async Task KeyExpand(int pageSize, bool navigationNextLink)
        {
            var parameters = new QueryParameters<Order>()
            {
                RequestUri = "Orders(1)?$expand=Customer,Items",
                Expression = t => t.Include(o => o.Customer).Include(o => o.Items).Where(o => o.Id == 1),
                NavigationNextLink = navigationNextLink,
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task KeyFilter(int pageSize)
        {
            var parameters = new QueryParameters<Order, OrderItem>()
            {
                RequestUri = "Orders(1)/Items?$filter=Count ge 2",
                Expression = t => t.Where(o => o.Id == 1).SelectMany(o => o.Items).Where(i => i.Count >= 2),
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task KeyMultipleNavigationOne(int pageSize)
        {
            var parameters = new QueryParameters<OrderItem, Customer>()
            {
                RequestUri = "OrderItems(1)/Order/Customer",
                Expression = t => t.Where(i => i.Id == 1).Select(i => i.Order.Customer),
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task KeyNavigationGroupBy(int pageSize)
        {
            var parameters = new QueryParameters<OrderItem, Object>()
            {
                RequestUri = "OrderItems(1)/Order?$apply=groupby((CustomerId), aggregate(Status with min as min))",
                Expression = t => t.Where(i => i.Id == 1).Select(i => i.Order).GroupBy(o => o.CustomerId).Select(g => new
                {
                    CustomerId = g.Key,
                    min = g.Min(a => a.Status)
                }),
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task KeyOrderBy(int pageSize)
        {
            var parameters = new QueryParameters<Order, OrderItem>()
            {
                RequestUri = "Orders(1)/Items?$orderby=Count,Price",
                Expression = t => t.Where(o => o.Id == 1).SelectMany(o => o.Items).OrderBy(i => i.Count).ThenBy(i => i.Price),
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task OrderByColumnsMissingInSelect(int pageSize)
        {
            var parameters = new QueryParameters<OrderItem, Object>()
            {
                RequestUri = "OrderItems?$select=Product,Id&$orderby=Count desc,Order/Customer/Name,Id desc",
                Expression = t => t.OrderByDescending(i => i.Count).ThenBy(i => i.Order.Customer.Name).ThenByDescending(i => i.Id).Select(i => new { i.Product, i.Id }),
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task OrderByColumnsMissingInSelectNavigationFirst(int pageSize)
        {
            var parameters = new QueryParameters<OrderItem, Object>()
            {
                RequestUri = "OrderItems?$select=Product,Id&$orderby=Order/Customer/Name desc,Count,Id desc",
                Expression = t => t.OrderByDescending(i => i.Order.Customer.Name).ThenBy(i => i.Count).ThenByDescending(i => i.Id).Select(i => new { i.Product, i.Id }),
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task OrderByDesc(int pageSize)
        {
            var parameters = new QueryParameters<OrderItem>()
            {
                RequestUri = "OrderItems?$orderby=Id desc,Count desc,Price desc",
                Expression = t => t.OrderByDescending(i => i.Id).ThenByDescending(i => i.Count).ThenByDescending(i => i.Price),
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task OrderByNavigation(int pageSize)
        {
            var parameters = new QueryParameters<OrderItem>()
            {
                RequestUri = "OrderItems?$orderby=Order/Customer/Sex desc,Order/Customer/Name,Id desc",
                Expression = t => t.OrderByDescending(i => i.Order.Customer.Sex).ThenBy(i => i.Order.Customer.Name).ThenByDescending(i => i.Id),
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0, false)]
        [InlineData(1, false)]
        [InlineData(0, true)]
        [InlineData(1, true)]
        public async Task Parameterization(int pageSize, bool navigationNextLink)
        {
            var parameters = new QueryParameters<Order>()
            {
                RequestUri = @"Orders?$filter=AltCustomerId eq 3 and CustomerId eq 4 and ((year(Date) eq 2016 and month(Date) gt 11 and day(Date) lt 20) or Date eq null) and contains(Name,'unknown') and Status eq OdataToEntity.Test.Model.OrderStatus'Unknown'
&$expand=Items($filter=(Count eq 0 or Count eq null) and (Price eq 0 or Price eq null) and (contains(Product,'unknown') or contains(Product,'null')) and OrderId gt -1 and Id ne 1)",
                Expression = t => t.Where(o => o.AltCustomerId == 3 && o.CustomerId == 4 && ((o.Date.Value.Year == 2016 && o.Date.Value.Month > 11 && o.Date.Value.Day < 20)) && o.Name.Contains("unknown") && o.Status == OrderStatus.Unknown)
                .Include(o => o.Items.Where(i => (i.Count == 0 || i.Count == null) && (i.Price == 0 || i.Price == null) && (i.Product.Contains("unknown") || i.Product.Contains("null")) && i.OrderId > -1 && i.Id != 1)),
                NavigationNextLink = navigationNextLink,
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Fact]
        [NotPerformanceCahe]
        public async Task ReferencedModels()
        {
            var parameters = new QueryParameters<Order>()
            {
                RequestUri = "Orders2",
                Expression = t => t
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task Select(int pageSize)
        {
            var parameters = new QueryParameters<Order, Object>()
            {
                RequestUri = "Orders?$select=AltCustomer,AltCustomerCountry,AltCustomerId,Customer,CustomerCountry,CustomerId,Date,Id,Items,Name,ShippingAddresses,Status&$orderby=Id",
                Expression = t => t.OrderBy(o => o.Id).Select(o => new { o.AltCustomer, o.AltCustomerCountry, o.AltCustomerId, o.Customer, o.CustomerCountry, o.CustomerId, o.Date, o.Id, o.Items, o.Name, o.ShippingAddresses, o.Status }),
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task SelectName(int pageSize)
        {
            var parameters = new QueryParameters<Order, Object>()
            {
                RequestUri = "Orders?$select=Name",
                Expression = t => t.Select(o => new { o.Name }),
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0, false)]
        [InlineData(1, false)]
        [InlineData(0, true)]
        [InlineData(1, true)]
        public async Task SelectNestedName(int pageSize, bool navigationNextLink)
        {
            var parameters = new QueryParameters<Order, Object>()
            {
                RequestUri = "Orders?$expand=Items($filter=OrderId eq 1;$select=Product)&$select=Name",
                Expression = t => t.Include(o => o.Items).Select(o => new { o.Name, Items = o.Items.Where(i => i.OrderId == 1).Select(i => new { i.Product }) }),
                NavigationNextLink = navigationNextLink,
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task Table(int pageSize)
        {
            var parameters = new QueryParameters<Order>()
            {
                RequestUri = "Orders?$orderby=Id",
                Expression = t => t.OrderBy(o => o.Id),
                PageSize = pageSize,
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Fact]
        public async Task TopSkip()
        {
            var parameters = new QueryParameters<Customer>()
            {
                RequestUri = "Customers?$orderby=Id&$top=3&$skip=2",
                Expression = t => t.OrderBy(c => c.Id).Skip(2).Take(3)
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }

        private DbFixtureInitDb Fixture { get; }
    }
}