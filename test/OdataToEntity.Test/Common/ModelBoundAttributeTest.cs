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
            String request = "Orders?$orderby=Id&$top=100";
            await SelectTest.OrdersCountItemsCount(Fixture, request, i => true, 0, false, OeModelBoundAttribute.Yes);
        }
        [Fact(Skip = SelectTest.SkipTest)]
        public async Task ExpandPage()
        {
            var parameters = new QueryParameters<Order, Object>()
            {
                RequestUri = "Orders?$orderby=Id&$top=100",
                Expression = t => t.OrderBy(o => o.Id).Take(2).Include(o => o.Customer).Include(o => o.Items.OrderBy(i => i.Id)).Select(o => new
                {
                    o.AltCustomerCountry,
                    o.AltCustomerId,
                    Customer = new { o.Customer.Name },
                    o.CustomerCountry,
                    o.CustomerId,
                    o.Date,
                    o.Id,
                    Items = o.Items.Take(2),
                    o.Name,
                    o.Status
                }),
                UseModelBoundAttribute = OeModelBoundAttribute.Yes
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }

        private DbFixtureInitDb Fixture { get; }
    }
}
