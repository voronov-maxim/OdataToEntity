using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.OData.Edm;
using OdataToEntity.EfCore;
using OdataToEntity.EfCore.DynamicDataContext;
using OdataToEntity.ModelBuilder;
using OdataToEntity.Test;
using OdataToEntity.Test.Model;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

public abstract class ModelBoundTest
{
    protected ModelBoundTest(DbFixtureInitDb fixture)
    {
    }
}

namespace OdataToEntity.Test.EfCore.SqlServer
{
    class Program
    {
        static async Task Main()
        {
            //PerformanceCacheTest.RunTest(100);
            //await new PLNull(new PLNull_DbFixtureInitDb()).Expand(0, false);

            IEdmModel edmModel = new OeEdmModelBuilder(new OrderDataAdapter(), new OeEdmModelMetadataProvider()).BuildEdmModel();
            var metadataProvider = new DynamicMetadataProvider(edmModel);

            DbContextOptions options = DynamicDbContext.CreateOptions();
            var typeDefinitionManager = DynamicTypeDefinitionManager.Create(options, metadataProvider);

            ////var dbContext = typeDefinitionManager.CreateDynamicDbContext();
            ////var orders = typeDefinitionManager.GetQueryable(dbContext, "Orders");
            ////var zzz = orders.Include("Items").Include("AltCustomer").Include("Customer").Include("ShippingAddresses").ToList();

            ////var orderItems = typeDefinitionManager.GetQueryable(dbContext, "OrderItems");
            ////var zzz2 = orderItems.Include("Order").ToList();

            ////dbContext = new DynamicDbContext(options, typeDefinitionManager);
            ////var zzz3 = new InternalDbSet<DynamicType01>(dbContext).ToList();

            var dataAdapter = new DynamicDataAdapter(typeDefinitionManager);
            var dynamicEdmModel = dataAdapter.BuildEdmModel();

            String schema = TestHelper.GetCsdlSchema(dynamicEdmModel);

            var parser = new OeParser(new Uri("http://dummy"), dynamicEdmModel);
            var stream = new MemoryStream();
            await parser.ExecuteGetAsync(new Uri("http://dummy/Orders?$expand=Customer,Items&$orderby=Id"), OeRequestHeaders.JsonDefault, stream, CancellationToken.None);
            stream.Position = 0;
            String result = new StreamReader(stream).ReadToEnd();
        }
    }
}