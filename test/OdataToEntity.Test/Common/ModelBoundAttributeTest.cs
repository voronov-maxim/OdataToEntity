using Microsoft.EntityFrameworkCore;
using Microsoft.OData;
using Microsoft.OData.Edm;
using OdataToEntity.Test.Model;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace OdataToEntity.Test
{
    public class ModelBoundAttributeDbFixture : DbFixtureInitDb
    {
        public ModelBoundAttributeDbFixture() : base(false, true, OeModelBoundAttribute.Yes)
        {
        }
    }

    public sealed class ModelBoundAttributeTest : IClassFixture<ModelBoundAttributeDbFixture>
    {
        public ModelBoundAttributeTest(ModelBoundAttributeDbFixture fixture)
        {
            fixture.Initalize().GetAwaiter().GetResult();
            Fixture = fixture;
        }

        [Fact(Skip = SelectTest.SkipTest)]
        public async Task Count()
        {
            String request = "Orders?$expand=Items($count=true)&$count=true";
            await SelectTest.OrdersCountItemsCount(Fixture, request, i => true, 0, false);
        }
        [Fact]
        public async Task CountFailed()
        {
            var parameters = new QueryParameters<Order, Object>()
            {
                RequestUri = "OrderItems?$count=true",
            };
            await Assert.ThrowsAsync<ODataErrorException>(async () => await Fixture.Execute(parameters).ConfigureAwait(false));
        }
        [Fact]
        public async Task CountNestedFailed()
        {
            var parameters = new QueryParameters<Customer, Object>()
            {
                RequestUri = "Customers?$expand=Orders($count=true)",
            };
            await Assert.ThrowsAsync<ODataErrorException>(async () => await Fixture.Execute(parameters).ConfigureAwait(false));
        }
        [Fact(Skip = SelectTest.SkipTest)]
        public async Task Expand()
        {
            var parameters = new QueryParameters<Order, Object>()
            {
                RequestUri = "Orders",
                Expression = t => t.OrderBy(o => o.Id).Include(o => o.Customer).ThenInclude(c => c.Orders).Include(o => o.Items.OrderBy(i => i.Id)).Select(o => new
                {
                    o.AltCustomerCountry,
                    o.AltCustomerId,
                    Customer = new
                    {
                        o.Customer.Address,
                        o.Customer.Country,
                        o.Customer.Id,
                        o.Customer.Name,
                        o.Customer.Orders,
                        o.Customer.Sex
                    },
                    o.CustomerCountry,
                    o.CustomerId,
                    o.Date,
                    o.Id,
                    o.Items,
                    o.Name,
                    o.Status
                }),
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Fact]
        public async Task ExpandFailed()
        {
            var parameters = new QueryParameters<Customer, Object>()
            {
                RequestUri = "Customers?$expand=AltOrders",
            };
            await Assert.ThrowsAsync<ODataErrorException>(async () => await Fixture.Execute(parameters).ConfigureAwait(false));
        }
        [Fact]
        public async Task ExpandSelectFailed()
        {
            var parameters = new QueryParameters<Customer, Object>()
            {
                RequestUri = "Customers?$select=AltOrders",
            };
            await Assert.ThrowsAsync<ODataErrorException>(async () => await Fixture.Execute(parameters).ConfigureAwait(false));
        }
        [Fact(Skip = SelectTest.SkipTest)]
        public async Task OrderBy()
        {
            var parameters = new QueryParameters<Order, Object>()
            {
                RequestUri = "Orders?$expand=Items($orderby=Id)&$orderby=Customer/Name",
                Expression = t => t.OrderBy(o => o.Customer.Name).Include(o => o.Customer).ThenInclude(c => c.Orders).Include(o => o.Items.OrderBy(i => i.Id)),
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Fact]
        public async Task OrderByFailed()
        {
            var parameters = new QueryParameters<Order, Object>()
            {
                RequestUri = "OrderItems?$orderby=Id",
            };
            await Assert.ThrowsAsync<ODataErrorException>(async () => await Fixture.Execute(parameters).ConfigureAwait(false));
        }
        [Fact]
        public async Task OrderByNestedFailed()
        {
            var parameters = new QueryParameters<Order, Object>()
            {
                RequestUri = "Orders?$expand=Items($orderby=Product)&$orderby=Customer/Name",
            };
            await Assert.ThrowsAsync<ODataErrorException>(async () => await Fixture.Execute(parameters).ConfigureAwait(false));
        }
        [Fact]
        public async Task TopFailed()
        {
            var parameters = new QueryParameters<Order, Object>()
            {
                RequestUri = "Orders?$top=100",
            };
            await Assert.ThrowsAsync<ODataErrorException>(async () => await Fixture.Execute(parameters).ConfigureAwait(false));
        }
        [Fact]
        public async Task TopNestedFailed()
        {
            var parameters = new QueryParameters<Order, Object>()
            {
                RequestUri = "Orders?$expand=Items($top=100)",
            };
            await Assert.ThrowsAsync<ODataErrorException>(async () => await Fixture.Execute(parameters).ConfigureAwait(false));
        }

        private DbFixtureInitDb Fixture { get; }
    }
}
