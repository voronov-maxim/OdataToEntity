using Microsoft.EntityFrameworkCore;
using Microsoft.OData;
using Microsoft.OData.UriParser;
using OdataToEntity.Test.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace OdataToEntity.Test
{
    public abstract class ModelBoundTest
    {
        protected ModelBoundTest(DbFixtureInitDb fixture)
        {
            fixture.Initalize().GetAwaiter().GetResult();
            Fixture = fixture;
        }

        [Fact(Skip = SelectTest.SkipTest)]
        public async Task Count()
        {
            String request = "Orders?$expand=Items($count=true)&$count=true";
            await SelectTest.OrdersCountItemsCount(Fixture, request, i => true, 0, false).ConfigureAwait(false);
        }
        [Fact]
        public async Task CountFailed()
        {
            var parameters = new QueryParameters<Order, Object>()
            {
                RequestUri = "OrderItems?$count=true",
            };

            try
            {
                await Fixture.Execute(parameters).ConfigureAwait(false);
            }
            catch (ODataErrorException e)
            {
                if (e.Message == "EntityType OrderItem not countable")
                    return;
            }
            Assert.Throws<ODataErrorException>(() => { });
        }
        [Fact]
        public async Task CountNestedFailed()
        {
            var parameters = new QueryParameters<Customer, Object>()
            {
                RequestUri = "Customers?$expand=Orders($count=true)",
            };

            try
            {
                await Fixture.Execute(parameters).ConfigureAwait(false);
            }
            catch (ODataErrorException e)
            {
                if (e.Message == "Navigation property Orders not countable")
                    return;
            }
            Assert.Throws<ODataErrorException>(() => { });
        }
        [Fact]
        public async Task ExpandFailed()
        {
            var parameters = new QueryParameters<Customer, Object>()
            {
                RequestUri = "Customers?$expand=AltOrders",
            };

            try
            {
                await Fixture.Execute(parameters).ConfigureAwait(false);
            }
            catch (ODataErrorException e)
            {
                if (e.Message == "Navigation property AltOrders not expandable")
                    return;
            }
            Assert.Throws<ODataErrorException>(() => { });
        }
        [Fact(Skip = SelectTest.SkipTest)]
        public async Task Filter()
        {
            var parameters = new QueryParameters<Order, Object>()
            {
                RequestUri = "Orders?$filter=Items/any(d:d/Count gt 2)",
                Expression = t => t.Where(o => o.Items.Any(i => i.Count > 2)).Include(o => o.Customer).ThenInclude(c => c.Orders).ThenInclude(o => o.Customer)
                    .Include(o => o.Customer).ThenInclude(c => c.Orders).ThenInclude(o => o.Items).Include(o => o.Items.OrderBy(i => i.Id)).Select(o =>
                    new
                    {
                        Customer = new
                        {
                            o.Customer.Name,
                            Orders = o.Customer.Orders.Select(z => new
                            {
                                Customer = new
                                {
                                    z.Customer.Name,
                                    z.Customer.Sex
                                },
                                Items = o.Items.Select(i => new
                                {
                                    i.Count,
                                    i.Price,
                                    i.Product
                                }),
                                z.Name,
                                z.Date,
                                z.Status
                            }),
                            o.Customer.Sex
                        },
                        o.Date,
                        Items = o.Items.Select(i => new
                        {
                            i.Count,
                            i.Price,
                            i.Product
                        }),
                        o.Name,
                        o.Status
                    })
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Fact]
        public async Task FilterFailed()
        {
            var parameters = new QueryParameters<Order, Object>()
            {
                RequestUri = "Orders?$filter=Items/any(d:d/Id eq 1)",
            };

            try
            {
                await Fixture.Execute(parameters).ConfigureAwait(false);
            }
            catch (ODataErrorException e)
            {
                if (e.Message == "Invalid filter by property")
                    return;
            }
            Assert.Throws<ODataErrorException>(() => { });
        }
        [Fact]
        public async Task FilterNestedFailed()
        {
            var parameters = new QueryParameters<Order, Object>()
            {
                RequestUri = "Orders?$expand=Items($filter=Id eq 1)",
            };

            try
            {
                await Fixture.Execute(parameters).ConfigureAwait(false);
            }
            catch (ODataErrorException e)
            {
                if (e.Message == "Navigation property Items not filterable")
                    return;
            }
            Assert.Throws<ODataErrorException>(() => { });
        }
        [Fact]
        public async Task NavigationNextLink()
        {
            String request = "Categories?$expand=Children";

            OeParser parser = Fixture.CreateParser(request, Fixture.ModelBoundProvider);
            ODataUri odataUri = Fixture.ParseUri(request);

            var response = new MemoryStream();
            await parser.ExecuteQueryAsync(odataUri, OeRequestHeaders.JsonDefault, response, CancellationToken.None).ConfigureAwait(false);
            response.Position = 0;

            var reader = new ResponseReader(parser.EdmModel);
            List<Object> categories = reader.Read(response).Cast<Object>().ToList();
            foreach (dynamic category in categories)
            {
                ResponseReader.NavigationInfo navigationInfo = reader.GetNavigationInfo(category.Children);
                String actual = Uri.UnescapeDataString(navigationInfo.NextPageLink.OriginalString);
                String expected = $"http://dummy/Categories?$filter=ParentId eq {category.Id}";
                Assert.Equal(expected, actual);
            }
        }
        [Fact(Skip = SelectTest.SkipTest)]
        public async Task OrderBy()
        {
            var parameters = new QueryParameters<Order, Object>()
            {
                RequestUri = "Orders?$expand=Items($orderby=Id)&$orderby=Customer/Name",
                Expression = t => t.OrderBy(o => o.Customer.Name).Include(o => o.Customer).ThenInclude(c => c.Orders).ThenInclude(o => o.Customer)
                    .Include(o => o.Customer).ThenInclude(c => c.Orders).ThenInclude(o => o.Items).Include(o => o.Items.OrderBy(i => i.Id)).Select(o =>
                    new
                    {
                        Customer = new
                        {
                            o.Customer.Name,
                            Orders = o.Customer.Orders.Select(z => new
                            {
                                Customer = new
                                {
                                    z.Customer.Name,
                                    z.Customer.Sex
                                },
                                Items = o.Items.Select(i => new
                                {
                                    i.Count,
                                    i.Price,
                                    i.Product
                                }),
                                z.Name,
                                z.Date,
                                z.Status
                            }),
                            o.Customer.Sex
                        },
                        o.Date,
                        Items = o.Items.Select(i => new
                        {
                            i.Count,
                            i.Price,
                            i.Product
                        }),
                        o.Name,
                        o.Status
                    })
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Fact]
        public async Task OrderByFailed()
        {
            var parameters = new QueryParameters<OrderItem, Object>()
            {
                RequestUri = "OrderItems?$orderby=Id",
            };
            try
            {
                await Fixture.Execute(parameters).ConfigureAwait(false);
            }
            catch (ODataErrorException e)
            {
                if (e.Message == "Invalid order by property")
                    return;
            }
            Assert.Throws<ODataErrorException>(() => { });
        }
        [Fact]
        public async Task OrderByNestedFailed()
        {
            var parameters = new QueryParameters<Order, Object>()
            {
                RequestUri = "Orders?$expand=Items($orderby=Product)&$orderby=Customer/Name",
            };

            try
            {
                await Fixture.Execute(parameters).ConfigureAwait(false);
            }
            catch (ODataErrorException e)
            {
                if (e.Message == "Navigation property Items not sortable")
                    return;
            }
            Assert.Throws<ODataErrorException>(() => { });
        }
        [Fact(Skip = SelectTest.SkipTest)]
        public async Task SelectExpand()
        {
            var parameters = new QueryParameters<Order, Object>()
            {
                RequestUri = "Orders",
                Expression = t => t.OrderBy(o => o.Id).Include(o => o.Customer).ThenInclude(c => c.Orders).ThenInclude(o => o.Customer)
                    .Include(o => o.Customer).ThenInclude(c => c.Orders).ThenInclude(o => o.Items).Include(o => o.Items.OrderBy(i => i.Id)).Select(o =>
                    new
                    {
                        Customer = new
                        {
                            o.Customer.Name,
                            Orders = o.Customer.Orders.Select(z => new
                            {
                                Customer = new
                                {
                                    z.Customer.Name,
                                    z.Customer.Sex
                                },
                                Items = o.Items.Select(i => new
                                {
                                    i.Count,
                                    i.Price,
                                    i.Product
                                }),
                                z.Name,
                                z.Date,
                                z.Status
                            }),
                            o.Customer.Sex
                        },
                        o.Date,
                        Items = o.Items.Select(i => new
                        {
                            i.Count,
                            i.Price,
                            i.Product
                        }),
                        o.Name,
                        o.Status
                    })
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Fact]
        public async Task SelectExpandFailed()
        {
            var parameters = new QueryParameters<Customer, Object>()
            {
                RequestUri = "Customers?$select=AltOrders",
            };

            try
            {
                await Fixture.Execute(parameters).ConfigureAwait(false);
            }
            catch (ODataErrorException e)
            {
                if (e.Message == "Structural property AltOrders not selectable")
                    return;
            }
            Assert.Throws<ODataErrorException>(() => { });
        }
        [Fact]
        public async Task SelectFailed()
        {
            var parameters = new QueryParameters<OrderItem, OrderItem>()
            {
                RequestUri = "OrderItems?$select=Id",
            };

            try
            {
                await Fixture.Execute(parameters).ConfigureAwait(false);
            }
            catch (ODataErrorException e)
            {
                if (e.Message == "Structural property Id not selectable")
                    return;
            }
            Assert.Throws<ODataErrorException>(() => { });
        }
        [Fact]
        public async Task SelectNestedFailed()
        {
            var parameters = new QueryParameters<Order, Order>()
            {
                RequestUri = "Orders?$expand=Items($select=Id)",
            };

            try
            {
                await Fixture.Execute(parameters).ConfigureAwait(false);
            }
            catch (ODataErrorException e)
            {
                if (e.Message == "Structural property Id not selectable")
                    return;
            }
            Assert.Throws<ODataErrorException>(() => { });
        }
        [Fact]
        public async Task TopFailed()
        {
            var parameters = new QueryParameters<Order, Object>()
            {
                RequestUri = "Orders?$top=100",
            };

            try
            {
                await Fixture.Execute(parameters).ConfigureAwait(false);
            }
            catch (ODataErrorException e)
            {
                if (e.Message == "EntityType Order not valid top")
                    return;
            }
            Assert.Throws<ODataErrorException>(() => { });
        }
        [Fact]
        public async Task TopNestedFailed()
        {
            var parameters = new QueryParameters<Order, Object>()
            {
                RequestUri = "Orders?$expand=Items($top=100)",
            };

            try
            {
                await Fixture.Execute(parameters).ConfigureAwait(false);
            }
            catch (ODataErrorException e)
            {
                if (e.Message == "Navigation property Items not valid top")
                    return;
            }
            Assert.Throws<ODataErrorException>(() => { });
        }

        private DbFixtureInitDb Fixture { get; }
    }
}
