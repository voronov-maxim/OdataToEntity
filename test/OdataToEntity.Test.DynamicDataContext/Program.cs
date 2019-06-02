using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.OData.Edm;
using OdataToEntity.EfCore;
using OdataToEntity.EfCore.DynamicDataContext;
using OdataToEntity.EfCore.DynamicDataContext.InformationSchema;
using OdataToEntity.EfCore.DynamicDataContext.Types;
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

namespace OdataToEntity.Test.DynamicDataContext
{
    class Program
    {
        static async Task Main()
        {
            //PerformanceCacheTest.RunTest(100);
            await new PLNull(new PLNull_DbFixtureInitDb()).Table(0);
            //new PLNull_ManyColumns(new PLNull_ManyColumnsFixtureInitDb()).Filter(1).GetAwaiter().GetResult();

            //IEdmModel edmModel = new OeEdmModelBuilder(new OrderDataAdapter(), new OeEdmModelMetadataProvider()).BuildEdmModel();
            //var metadataProvider = new EdmDynamicMetadataProvider(edmModel);
            DbContextOptions options = OrderContextOptions.Create<DynamicDbContext>(true);
            var metadataProvider = new DbDynamicMetadataProvider(@"Server=.\sqlexpress;Initial Catalog=OdataToEntity;Trusted_Connection=Yes;", true);
            //metadataProvider.EnumMappings = GetEnums();
            metadataProvider.TableMappings = GetMappings();

            var typeDefinitionManager = DynamicTypeDefinitionManager.Create(metadataProvider);

            //var dbContext = typeDefinitionManager.CreateDynamicDbContext();
            //var orders = typeDefinitionManager.GetQueryable(dbContext, "Orders");
            //var zzz = orders.Include("Items").Include("AltCustomer").Include("Customer").Include("ShippingAddresses").ToList();

            //var orderItems = typeDefinitionManager.GetQueryable(dbContext, "OrderItems");
            //var zzz2 = orderItems.Include("Order").ToList();

            //dbContext = new DynamicDbContext(options, typeDefinitionManager);
            //var zzz3 = new InternalDbSet<DynamicType01>(dbContext).ToList();

            var dataAdapter = new DynamicDataAdapter(typeDefinitionManager);
            var dynamicEdmModel = dataAdapter.BuildEdmModel();

            String schema = TestHelper.GetCsdlSchema(dynamicEdmModel);

            var parser = new OeParser(new Uri("http://dummy"), dynamicEdmModel);
            var stream = new MemoryStream();
            //await parser.ExecuteGetAsync(new Uri("http://dummy/Orders?$expand=Customer,Items&$orderby=Id"), OeRequestHeaders.JsonDefault, stream, CancellationToken.None);
            await parser.ExecuteGetAsync(new Uri("http://dummy/Orders"), OeRequestHeaders.JsonDefault, stream, CancellationToken.None);
            stream.Position = 0;
            String result = new StreamReader(stream).ReadToEnd();
        }

        public static TableMapping[] GetMappings()
        {
            return new TableMapping[]
            {
                new TableMapping("dbo.Categories"),
                new TableMapping("dbo.Customers")
                {
                    Navigations = new []
                    {
                        new NavigationMapping("FK_Orders_Customers", "Orders"),
                        new NavigationMapping("FK_Orders_AltCustomers", "AltOrders"),
                        new NavigationMapping(null, "ShippingAddresses") { ManyToManyTarget = "ShippingAddresses" }
                    }
                },
                new TableMapping("dbo.CustomerShippingAddress")
                {
                    Navigations = new []
                    {
                        new NavigationMapping("FK_CustomerShippingAddress_Customers", "Customer"),
                        new NavigationMapping("FK_CustomerShippingAddress_ShippingAddresses", "ShippingAddress")
                    }
                },
                new TableMapping("dbo.ManyColumns"),
                new TableMapping("dbo.Orders")
                {
                    Navigations = new []
                    {
                        new NavigationMapping("FK_OrderItem_Order", "Items"),
                        new NavigationMapping("FK_Orders_AltCustomers", "AltCustomer"),
                        new NavigationMapping("FK_Orders_Customers", "Customer")
                    }
                },
                new TableMapping("dbo.OrderItems")
                {
                    Navigations = new []
                    {
                        new NavigationMapping("FK_OrderItem_Order", "Order")
                    }
                },
                new TableMapping("dbo.ShippingAddresses")
                {
                    Navigations = new []
                    {
                        new NavigationMapping("FK_CustomerShippingAddress_ShippingAddresses", "CustomerShippingAddresses"),
                    }
                }
            };
        }
    }
}