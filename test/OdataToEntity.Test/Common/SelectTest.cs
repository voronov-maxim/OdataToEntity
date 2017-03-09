using Microsoft.EntityFrameworkCore;
using OdataToEntity.Test.Model;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace OdataToEntity.Test
{
    public sealed partial class SelectTest : IClassFixture<DbFixtureInitDb>
    {
        private readonly DbFixtureInitDb _fixture;

        public SelectTest(DbFixtureInitDb fixture)
        {
            fixture.Initalize();
            _fixture = fixture;
        }

        [Fact]
        public async Task ApplyFilter()
        {
            var parameters = new QueryParameters<OrderItem>()
            {
                RequestUri = "OrderItems?$apply=filter(Order/Status eq OdataToEntity.Test.Model.OrderStatus'Processing')",
                Expression = t => t.Where(i => i.Order.Status == OrderStatus.Processing)
            };
            await Fixture.Execute(parameters);
        }
        [Fact]
        public async Task ApplyFilterGroupBy()
        {
            var parameters = new QueryParameters<Order, Object>()
            {
                RequestUri = "Orders?$apply=filter(Status eq OdataToEntity.Test.Model.OrderStatus'Unknown')/groupby((Name), aggregate(Id with countdistinct as cnt))",
                Expression = t => t.Where(o => o.Status == OrderStatus.Unknown).GroupBy(o => o.Name).Select(g => new { Name = g.Key, cnt = g.Count() })
            };
            await Fixture.Execute(parameters);
        }
        [Fact]
        public async Task ApplyGroupBy()
        {
            var parameters = new QueryParameters<OrderItem, Object>()
            {
                RequestUri = "OrderItems?$apply=groupby((Product))",
                Expression = t => t.GroupBy(i => i.Product).Select(g => new { Product = g.Key })
            };
            await Fixture.Execute(parameters);
        }
        [Fact]
        public async Task ApplyGroupByAggregate()
        {
            var parameters = new QueryParameters<OrderItem, Object>()
            {
                RequestUri = "OrderItems?$apply=groupby((OrderId, Order/Status), aggregate(Price with average as avg, Product with countdistinct as cnt, Price with max as max, Order/Status with max as max_status, Price with min as min, Price with sum as sum))",
                Expression = t => t.GroupBy(i => new { i.OrderId, i.Order.Status }).Select(g => new
                {
                    OrderId = g.Key.OrderId,
                    Order_Status = g.Key.Status,
                    avg = g.Average(i => i.Price),
                    cnt = g.Select(i => i.Product).Distinct().Count(),
                    max = g.Max(i => i.Price),
                    max_status = g.Max(i => i.Order.Status),
                    min = g.Min(i => i.Price),
                    sum = g.Sum(i => i.Price)
                })
            };
            await Fixture.Execute(parameters);
        }
        [Fact]
        public async Task ApplyGroupByAggregateFilter()
        {
            var parameters = new QueryParameters<OrderItem, Object>()
            {
                RequestUri = "OrderItems?$apply=groupby((OrderId), aggregate(Price with sum as sum))/filter(OrderId eq 2 and sum ge 4)",
                Expression = t => t.GroupBy(i => i.OrderId).Select(g => new
                {
                    OrderId = g.Key,
                    sum = g.Sum(i => i.Price)
                }).Where(a => a.OrderId == 2 && a.sum >= 4)
            };
            await Fixture.Execute(parameters);
        }
        [Fact]
        public async Task ApplyGroupByAggregateFilterOrdinal()
        {
            var parameters = new QueryParameters<OrderItem, Object>()
            {
                RequestUri = "OrderItems?$apply=groupby((OrderId), aggregate(Price with sum as sum))&$filter=OrderId eq 2",
                Expression = t => t.GroupBy(i => i.OrderId).Select(g => new
                {
                    OrderId = g.Key,
                    sum = g.Sum(i => i.Price)
                }).Where(a => a.OrderId == 2)
            };
            await Fixture.Execute(parameters);
        }
        [Fact]
        public async Task ApplyGroupByFilter()
        {
            var parameters = new QueryParameters<OrderItem, Object>()
            {
                RequestUri = "OrderItems?$apply=groupby((OrderId, Order/Name))/filter(OrderId eq 1 and Order/Name eq 'Order 1')",
                Expression = t => t.GroupBy(i => new { i.OrderId, i.Order.Name })
                    .Where(g => g.Key.OrderId == 1 && g.Key.Name == "Order 1")
                    .Select(g => new { OrderId = g.Key.OrderId, Order_Name = g.Key.Name })
            };
            await Fixture.Execute(parameters);
        }
        [Fact]
        public async Task ApplyGroupByMul()
        {
            var parameters = new QueryParameters<OrderItem, Object>()
            {
                RequestUri = "OrderItems?$apply=groupby((OrderId), aggregate(Price mul Count with sum as sum))",
                Expression = t => t.GroupBy(i => i.OrderId).Select(g => new { OrderId = g.Key, sum = g.Sum(i => i.Price * i.Count) })
            };
            await Fixture.Execute(parameters);
        }
        [Fact]
        public async Task ApplyGroupByOrderBy()
        {
            var parameters = new QueryParameters<OrderItem, Object>()
            {
                RequestUri = "OrderItems?$apply=groupby((OrderId, Order/Name))&$orderby=OrderId desc, Order/Name",
                Expression = t => t.GroupBy(i => new { i.OrderId, i.Order.Name })
                    .Select(g => new { OrderId = g.Key.OrderId, Order_Name = g.Key.Name })
                    .OrderByDescending(a => a.OrderId).ThenBy(a => a.Order_Name)
            };
            await Fixture.Execute(parameters);
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
            await Fixture.Execute(parameters);
        }
        [Fact]
        public async Task ApplyGroupBySkip()
        {
            var parameters = new QueryParameters<OrderItem, Object>()
            {
                RequestUri = "OrderItems?$apply=groupby((OrderId))&$orderby=OrderId&$skip=1",
                Expression = t => t.GroupBy(i => i.OrderId).OrderBy(g => g.Key).Skip(1).Select(g => new { OrderId = g.Key })
            };
            await Fixture.Execute(parameters);
        }
        [Fact]
        public async Task ApplyGroupByTop()
        {
            var parameters = new QueryParameters<OrderItem, Object>()
            {
                RequestUri = "OrderItems?$apply=groupby((OrderId))&$top=1",
                Expression = t => t.GroupBy(i => i.OrderId).Take(1).Select(g => new { OrderId = g.Key })
            };
            await Fixture.Execute(parameters);
        }
        [Fact]
        public async Task Count()
        {
            var parameters = new QueryParametersScalar<Order, int>()
            {
                RequestUri = "Orders/$count",
                Expression = t => t.Count(),
            };
            await Fixture.Execute(parameters);
        }
        [Fact]
        public async Task Expand()
        {
            var parameters = new QueryParameters<Order>()
            {
                RequestUri = "Orders?$expand=Customer,Items",
                Expression = t => t.Include(o => o.Customer).Include(o => o.Items),
            };
            await Fixture.Execute(parameters);
        }
        [Fact]
        public async Task ExpandAndSelect()
        {
            var parameters = new QueryParameters<Order, Object>()
            {
                RequestUri = "Orders?$expand=AltCustomer,Customer,Items&$select=AltCustomerId,CustomerId,Date,Id,Name,Status",
                Expression = t => t.Select(o => new { o.AltCustomer, o.Customer, o.Items, o.AltCustomerId, o.CustomerId, o.Date, o.Id, o.Name, o.Status }),
            };
            await Fixture.Execute(parameters);
        }
        [Fact]
        public async Task ExpandExpandFilter()
        {
            var parameters = new QueryParameters<Customer, Customer>()
            {
                RequestUri = "Customers?$expand=AltOrders($expand=Items($filter=contains(Product,'unknown'))),Orders($expand=Items($filter=contains(Product,'unknown')))",
                Expression = t => t.Include(c => c.AltOrders).Include(c => c.Orders).ThenInclude(o => o.Items.Where(i => i.Product.Contains("unknown")))
            };
            await Fixture.Execute(parameters);
        }
        [Fact]
        public async Task ExpandExpandMany()
        {
            var parameters = new QueryParameters<Customer, Customer>()
            {
                RequestUri = "Customers?$expand=AltOrders($expand=Items),Orders($expand=Items)",
                Expression = t => t.Include(c => c.AltOrders).Include(c => c.Orders).ThenInclude(o => o.Items)
            };
            await Fixture.Execute(parameters);
        }
        [Fact]
        public async Task ExpandExpandOne()
        {
            var parameters = new QueryParameters<OrderItem, OrderItem>()
            {
                RequestUri = "OrderItems?$expand=Order($expand=AltCustomer,Customer)",
                Expression = t => t.Include(i => i.Order).ThenInclude(o => o.AltCustomer).Include(i => i.Order).ThenInclude(o => o.Customer)
            };
            await Fixture.Execute(parameters);
        }
        [Fact]
        public async Task ExpandExpandOrderBy()
        {
            var parameters = new QueryParameters<Customer, Customer>()
            {
                RequestUri = "Customers?$expand=Orders($expand=Items($orderby=Id desc))",
                Expression = t => t.Include(c => c.Orders).ThenInclude(o => o.Items.OrderByDescending(i => i.Id))
            };
            await Fixture.Execute(parameters);
        }
        [Fact]
        public async Task ExpandExpandSkipTop()
        {
            var parameters = new QueryParameters<Customer, Customer>()
            {
                RequestUri = "Customers?$orderby=Id&$skip=1&$top=3&$expand=AltOrders($expand=Items($top=1)),Orders($expand=Items($top=1))",
                Expression = t => t.OrderBy(o => o.Id).Skip(1).Take(3).Include(c => c.AltOrders).Include(c => c.Orders).ThenInclude(o => o.Items.Take(1))
            };
            await Fixture.Execute(parameters);
        }
        [Fact]
        public async Task ExpandFilter()
        {
            var parameters = new QueryParameters<Customer, Customer>()
            {
                RequestUri = "Customers?$expand=Orders($filter=Status eq OdataToEntity.Test.Model.OrderStatus'Processing')",
                Expression = t => t.Include(c => c.Orders.Where(o => o.Status == OrderStatus.Processing))
            };
            await Fixture.Execute(parameters);
        }
        [Fact]
        public async Task ExpandInverseProperty()
        {
            var parameters = new QueryParameters<Customer, Customer>()
            {
                RequestUri = "Customers?$expand=AltOrders,Orders",
                Expression = t => t.Include(c => c.AltOrders).Include(c => c.Orders)
            };
            await Fixture.Execute(parameters);
        }
        [Fact]
        public async Task ExpandNestedSelect()
        {
            var parameters = new QueryParameters<Customer, Customer>()
            {
                RequestUri = "Customers?$expand=Orders($select=AltCustomerId,CustomerId,Date,Id,Name,Status)",
                Expression = t => t.Include(c => c.Orders)
            };
            await Fixture.Execute(parameters);
        }
        [Fact]
        public async Task ExpandStar()
        {
            var parameters = new QueryParameters<Order>()
            {
                RequestUri = "Orders?$expand=*",
                Expression = t => t.Include(o => o.AltCustomer).Include(o => o.Customer).Include(o => o.Items)
            };
            await Fixture.Execute(parameters);
        }
        [Fact]
        public async Task FilterAll()
        {
            var parameters = new QueryParameters<Order>()
            {
                RequestUri = "Orders?$filter=Items/all(d:d/Price ge 2.1)",
                Expression = t => t.Where(o => o.Items.All(i => i.Price >= 2.1m))
            };
            await Fixture.Execute(parameters);
        }
        [Fact]
        public async Task FilterAny()
        {
            var parameters = new QueryParameters<Order>()
            {
                RequestUri = "Orders?$filter=Items/any(d:d/Count gt 2)",
                Expression = t => t.Where(o => o.Items.Any(i => i.Count > 2))
            };
            await Fixture.Execute(parameters);
        }
        [Fact]
        public async Task FilterApplyGroupBy()
        {
            var parameters = new QueryParameters<Order, Object>()
            {
                RequestUri = "Orders?$filter=Status eq OdataToEntity.Test.Model.OrderStatus'Unknown'&$apply=groupby((Name), aggregate(Id with countdistinct as cnt))",
                Expression = t => t.Where(o => o.Status == OrderStatus.Unknown).GroupBy(o => o.Name).Select(g => new { Name = g.Key, cnt = g.Count() })
            };
            await Fixture.Execute(parameters);
        }
        [Fact]
        public async Task FilterCount()
        {
            var parameters = new QueryParameters<Order>()
            {
                RequestUri = "Orders?$filter=Items/$count gt 2",
                Expression = t => t.Where(o => o.Items.Count() > 2)
            };
            await Fixture.Execute(parameters);
        }
        [Fact]
        public async Task FilterDateTimeOffset()
        {
            var parameters = new QueryParameters<Order>()
            {
                RequestUri = "Orders?$filter=Date ge 2016-07-04T19:10:10.8237573%2B03:00",
                Expression = t => t.Where(o => o.Date >= DateTimeOffset.Parse("2016-07-04T19:10:10.8237573+03:00"))
            };
            await Fixture.Execute(parameters);
        }
        [Fact]
        public async Task FilterDateTimeOffsetYearMonthDay()
        {
            var parameters = new QueryParameters<Order>()
            {
                RequestUri = "Orders?$filter=year(Date) eq 2016 and month(Date) gt 3 and day(Date) lt 20",
                Expression = t => t.Where(o => o.Date.GetValueOrDefault().Year == 2016 && o.Date.GetValueOrDefault().Month > 3 && o.Date.GetValueOrDefault().Day < 20)
            };
            await Fixture.Execute(parameters);
        }
        [Fact]
        public async Task FilterDateTimeOffsetNull()
        {
            var parameters = new QueryParameters<Order>()
            {
                RequestUri = "Orders?$filter=Date eq null",
                Expression = t => t.Where(o => o.Date == null)
            };
            await Fixture.Execute(parameters);
        }
        [Fact]
        public async Task FilterDecimal()
        {
            var parameters = new QueryParameters<OrderItem>()
            {
                RequestUri = "OrderItems?$filter=Price gt 2",
                Expression = t => t.Where(i => i.Price > 2)
            };
            await Fixture.Execute(parameters);
        }
        [Fact]
        public async Task FilterDecimalNull()
        {
            var parameters = new QueryParameters<OrderItem>()
            {
                RequestUri = "OrderItems?$filter=Price eq null",
                Expression = t => t.Where(i => i.Price == null)
            };
            await Fixture.Execute(parameters);
        }
        [Fact]
        public async Task FilterEnum()
        {
            var parameters = new QueryParameters<Customer>()
            {
                RequestUri = "Customers?$filter=Sex eq OdataToEntity.Test.Model.Sex'Female'",
                Expression = t => t.Where(c => c.Sex == Sex.Female)
            };
            await Fixture.Execute(parameters);
        }
        [Fact]
        public async Task FilterEnumNull()
        {
            var parameters = new QueryParameters<Customer>()
            {
                RequestUri = "Customers?$filter=Sex eq null",
                Expression = t => t.Where(c => c.Sex == null)
            };
            await Fixture.Execute(parameters);
        }
        [Fact]
        public async Task FilterEnumNotNullAndStringNotNull()
        {
            var parameters = new QueryParameters<Customer>()
            {
                RequestUri = "Customers?$filter=Sex ne null and Address ne null",
                Expression = t => t.Where(c => c.Sex != null && c.Address != null)
            };
            await Fixture.Execute(parameters);
        }
        [Fact]
        public async Task FilterEnumNullAndStringNotNull()
        {
            var parameters = new QueryParameters<Customer>()
            {
                RequestUri = "Customers?$filter=Sex eq null and Address ne null",
                Expression = t => t.Where(c => c.Sex == null && c.Address != null)
            };
            await Fixture.Execute(parameters);
        }
        [Fact]
        public async Task FilterEnumNullAndStringNull()
        {
            var parameters = new QueryParameters<Customer>()
            {
                RequestUri = "Customers?$filter=Sex eq null and Address eq null",
                Expression = t => t.Where(c => c.Sex == null && c.Address == null)
            };
            await Fixture.Execute(parameters);
        }
        [Fact]
        public async Task FilterInt()
        {
            var parameters = new QueryParameters<OrderItem>()
            {
                RequestUri = "OrderItems?$filter=Count ge 2",
                Expression = t => t.Where(i => i.Count >= 2)
            };
            await Fixture.Execute(parameters);
        }
        [Fact]
        public async Task FilterIntNull()
        {
            var parameters = new QueryParameters<OrderItem>()
            {
                RequestUri = "OrderItems?$filter=Count eq null",
                Expression = t => t.Where(i => i.Count == null)
            };
            await Fixture.Execute(parameters);
        }
        [Fact]
        public async Task FilterNavigation()
        {
            var parameters = new QueryParameters<OrderItem>()
            {
                RequestUri = "OrderItems?$filter=Order/Customer/Name eq 'Ivan'",
                Expression = t => t.Where(i => i.Order.Customer.Name == "Ivan")
            };
            await Fixture.Execute(parameters);
        }
        [Fact]
        public async Task FilterString()
        {
            var parameters = new QueryParameters<Customer>()
            {
                RequestUri = "Customers?$filter=Address eq 'Tula'",
                Expression = t => t.Where(c => c.Address == "Tula")
            };
            await Fixture.Execute(parameters);
        }
        [Fact]
        public async Task FilterStringConcat()
        {
            var parameters = new QueryParameters<Customer>()
            {
                RequestUri = "Customers?$filter=concat(concat(Name,' hello'),' world') eq 'Ivan hello world'",
                Expression = t => t.Where(c => (c.Name + " hello world") == "Ivan hello world")
            };
            await Fixture.Execute(parameters);
        }
        [Fact]
        public async Task FilterStringContains()
        {
            var parameters = new QueryParameters<Customer>()
            {
                RequestUri = "Customers?$filter=contains(Name, 'sh')",
                Expression = t => t.Where(c => c.Name.Contains("sh"))
            };
            await Fixture.Execute(parameters);
        }
        [Fact]
        public async Task FilterStringEndsWith()
        {
            var parameters = new QueryParameters<Customer>()
            {
                RequestUri = "Customers?$filter=endswith(Name, 'asha')",
                Expression = t => t.Where(c => c.Name.EndsWith("asha"))
            };
            await Fixture.Execute(parameters);
        }
        [Fact]
        public async Task FilterStringLength()
        {
            var parameters = new QueryParameters<Customer>()
            {
                RequestUri = "Customers?$filter=length(Name) eq 5",
                Expression = t => t.Where(c => c.Name.Length == 5)
            };
            await Fixture.Execute(parameters);
        }
        [Fact]
        public async Task FilterStringIndexOf()
        {
            var parameters = new QueryParameters<Customer>()
            {
                RequestUri = "Customers?$filter=indexof(Name, 'asha') eq 1",
                Expression = t => t.Where(c => c.Name.IndexOf("asha") == 1)
            };
            await Fixture.Execute(parameters);
        }
        [Fact]
        public async Task FilterStringStartsWith()
        {
            var parameters = new QueryParameters<Customer>()
            {
                RequestUri = "Customers?$filter=startswith(Name, 'S')",
                Expression = t => t.Where(c => c.Name.StartsWith("S"))
            };
            await Fixture.Execute(parameters);
        }
        [Fact]
        public async Task FilterStringSubstring()
        {
            var parameters = new QueryParameters<Customer>()
            {
                RequestUri = "Customers?$filter=substring(Name, 1, 1) eq substring(Name, 4)",
                Expression = t => t.Where(c => c.Name.Substring(1, 1) == c.Name.Substring(4))
            };
            await Fixture.Execute(parameters);
        }
        [Fact]
        public async Task FilterStringToLower()
        {
            var parameters = new QueryParameters<Customer>()
            {
                RequestUri = "Customers?$filter=tolower(Name) eq 'sasha'",
                Expression = t => t.Where(c => c.Name.ToLower() == "sasha")
            };
            await Fixture.Execute(parameters);
        }
        [Fact]
        public async Task FilterStringToUpper()
        {
            var parameters = new QueryParameters<Customer>()
            {
                RequestUri = "Customers?$filter=toupper(Name) eq 'SASHA'",
                Expression = t => t.Where(c => c.Name.ToUpper() == "SASHA")
            };
            await Fixture.Execute(parameters);
        }
        [Fact]
        public async Task FilterStringTrim()
        {
            var parameters = new QueryParameters<Customer>()
            {
                RequestUri = "Customers?$filter=trim(concat(Name, ' ')) eq trim(Name)",
                Expression = t => t.Where(c => (c.Name + " ").Trim() == c.Name.Trim())
            };
            await Fixture.Execute(parameters);
        }
        [Fact]
        public async Task KeyExpand()
        {
            var parameters = new QueryParameters<Order>()
            {
                RequestUri = "Orders(1)?$expand=Customer,Items",
                Expression = t => t.Include(o => o.Customer).Include(o => o.Items).Where(o => o.Id == 1)
            };
            await Fixture.Execute(parameters);
        }
        [Fact]
        public async Task KeyFilter()
        {
            var parameters = new QueryParameters<Order, OrderItem>()
            {
                RequestUri = "Orders(1)/Items?$filter=Count ge 2",
                Expression = t => t.Where(o => o.Id == 1).SelectMany(o => o.Items).Where(i => i.Count >= 2)
            };
            await Fixture.Execute(parameters);
        }
        [Fact]
        public async Task KeyMultipleNavigationOne()
        {
            var parameters = new QueryParameters<OrderItem, Customer>()
            {
                RequestUri = "OrderItems(1)/Order/Customer",
                Expression = t => t.Where(i => i.Id == 1).Select(i => i.Order.Customer)
            };
            await Fixture.Execute(parameters);
        }
        [Fact]
        public async Task KeyNavigationGroupBy()
        {
            var parameters = new QueryParameters<OrderItem, Object>()
            {
                RequestUri = "OrderItems(1)/Order?$apply=groupby((CustomerId), aggregate(Status with min as min))",
                Expression = t => t.Where(i => i.Id == 1).Select(i => i.Order).GroupBy(o => o.Id).Select(g => new
                {
                    CustomerId = g.Key,
                    min = g.Min(a => a.Status)
                })
            };
            await Fixture.Execute(parameters);
        }
        [Fact]
        public async Task KeyOrderBy()
        {
            var parameters = new QueryParameters<Order, OrderItem>()
            {
                RequestUri = "Orders(1)/Items?$orderby=Count,Price",
                Expression = t => t.Where(o => o.Id == 1).SelectMany(o => o.Items).OrderBy(i => i.Count).ThenBy(i => i.Price)
            };
            await Fixture.Execute(parameters);
        }
        [Fact]
        public async Task OrderByDesc()
        {
            var parameters = new QueryParameters<OrderItem>()
            {
                RequestUri = "OrderItems?$orderby=Id desc,Count desc,Price desc",
                Expression = t => t.OrderByDescending(i => i.Id).ThenByDescending(i => i.Count).ThenByDescending(i => i.Price)
            };
            await Fixture.Execute(parameters);
        }
        [Fact]
        public async Task OrderByNavigation()
        {
            var parameters = new QueryParameters<OrderItem>()
            {
                RequestUri = "OrderItems?$orderby=Order/Customer/Sex desc,Order/Customer/Name,Id desc",
                Expression = t => t.OrderByDescending(i => i.Order.Customer.Sex).ThenBy(i => i.Order.Customer.Name).ThenByDescending(i => i.Id)
            };
            await Fixture.Execute(parameters);
        }
        [Fact]
        public async Task Parameterization()
        {
            var parameters = new QueryParameters<Order>()
            {
                RequestUri = @"Orders?$filter=AltCustomerId eq 3 and CustomerId eq 4 and ((year(Date) eq 2016 and month(Date) gt 11 and day(Date) lt 20) or Date eq null) and contains(Name,'unknown') and Status eq OdataToEntity.Test.Model.OrderStatus'Unknown'
&$expand=Items($filter=(Count eq 0 or Count eq null) and (Price eq 0 or Price eq null) and (contains(Product,'unknown') or contains(Product,'null')) and OrderId gt -1 and Id ne 1)",
                Expression = t => t.Where(o => o.AltCustomerId == 3 && o.CustomerId == 4 && ((o.Date.GetValueOrDefault().Year == 2016 && o.Date.GetValueOrDefault().Month > 11 && o.Date.GetValueOrDefault().Day < 20) || o.Date == null) && o.Name.Contains("unknown") && o.Status == OrderStatus.Unknown)
                .Include(o => o.Items.Where(i => (i.Count == 0 || i.Count == null) && (i.Price == 0 || i.Price == null) && (i.Product.Contains("unknown") || i.Product.Contains("null")) && i.OrderId > -1 && i.Id != 1))
            };
            await Fixture.Execute(parameters);
        }
        [Fact]
        public async Task Select()
        {
            var parameters = new QueryParameters<Order, Object>()
            {
                RequestUri = "Orders?$select=AltCustomer,AltCustomerId,Customer,CustomerId,Date,Id,Items,Name,Status",
                Expression = t => t.Select(o => new { o.AltCustomer, o.AltCustomerId, o.Customer, o.CustomerId, o.Date, o.Id, o.Items, o.Name, o.Status })
            };
            await Fixture.Execute(parameters);
        }
        [Fact]
        public async Task SelectName()
        {
            var parameters = new QueryParameters<Order, Object>()
            {
                RequestUri = "Orders?$select=Name",
                Expression = t => t.Select(o => new { o.Name })
            };
            await Fixture.Execute(parameters);
        }
        [Fact]
        public async Task Table()
        {
            var parameters = new QueryParameters<Customer>()
            {
                RequestUri = "Customers",
                Expression = t => t
            };
            await Fixture.Execute(parameters);
        }
        [Fact]
        public async Task TopSkip()
        {
            var parameters = new QueryParameters<Customer>()
            {
                RequestUri = "Customers?$orderby=Id&$top=3&$skip=2",
                Expression = t => t.OrderBy(c => c.Id).Skip(2).Take(3)
            };
            await Fixture.Execute(parameters);
        }

        private DbFixtureInitDb Fixture => _fixture;
    }
}
