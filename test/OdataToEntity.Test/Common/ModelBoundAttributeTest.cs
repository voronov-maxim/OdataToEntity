using Microsoft.EntityFrameworkCore;
using OdataToEntity.Test.Model;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace OdataToEntity.Test
{
    public class ModelBoundAttributeDbFixture : DbFixtureInitDb
    {
        public ModelBoundAttributeDbFixture() : base(true, true, true)
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

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task Expand(int pageSize)
        {
            var parameters = new QueryParameters<Order, Object>()
            {
                RequestUri = "Orders",
                Expression = t => t.Include(o => o.Customer).Select(o => new
                {
                    o.AltCustomerCountry,
                    o.AltCustomerId,
                    Customer = new { o.Customer.Name },
                    o.CustomerCountry,
                    o.CustomerId,
                    o.Date,
                    o.Id,
                    o.Name,
                    o.Status
                }),
                PageSize = pageSize,
                UseModelBoundAttribute = true
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }

        private DbFixtureInitDb Fixture { get; }
    }
}
