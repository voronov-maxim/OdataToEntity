using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.OData.Edm;
using OdataToEntity.EfCore;
using OdataToEntity.EfCore.DynamicDataContext;
using OdataToEntity.EfCore.DynamicDataContext.InformationSchema;
using OdataToEntity.EfCore.DynamicDataContext.ModelBuilder;
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
            await new PLNull(new PLNull_DbFixtureInitDb()).ExpandExpandMany(0, false);
            //new ProcedureTest().TableFunction_get().GetAwaiter().GetResult();
            //new PLNull_ManyColumns(new PLNull_ManyColumnsFixtureInitDb()).Filter(1).GetAwaiter().GetResult();

            //IEdmModel edmModel = new OeEdmModelBuilder(new OrderDataAdapter(), new OeEdmModelMetadataProvider()).BuildEdmModel();
            //var metadataProvider = new EdmDynamicMetadataProvider(edmModel);
            InformationSchemaMapping informationSchemaMapping = GetMappings();
            //var informationSchema = new SqlServerSchema(CreateOptionsSqlServer(true));
            var informationSchema = new PostgreSqlSchema(CreateOptionsPostgreSql(true));

            EdmModel dynamicEdmModel;
            using (var metadataProvider = new DynamicMetadataProvider(informationSchema, informationSchemaMapping))
            {
                DynamicTypeDefinitionManager typeDefinitionManager = DynamicTypeDefinitionManager.Create(metadataProvider);
                var dataAdapter = new DynamicDataAdapter(typeDefinitionManager);
                dynamicEdmModel = dataAdapter.BuildEdmModel(metadataProvider);
            }
            String csdlSchema = TestHelper.GetCsdlSchema(dynamicEdmModel);

            //var dbContext = typeDefinitionManager.CreateDynamicDbContext();
            //var orders = typeDefinitionManager.GetQueryable(dbContext, "Orders");
            //var zzz = orders.Include("Items").Include("AltCustomer").Include("Customer").Include("ShippingAddresses").ToList();

            //var orderItems = typeDefinitionManager.GetQueryable(dbContext, "OrderItems");
            //var zzz2 = orderItems.Include("Order").ToList();

            //dbContext = new DynamicDbContext(options, typeDefinitionManager);
            //var zzz3 = new InternalDbSet<DynamicType01>(dbContext).ToList();

            var parser = new OeParser(new Uri("http://dummy"), dynamicEdmModel);
            var stream = new MemoryStream();
            //await parser.ExecuteGetAsync(new Uri("http://dummy/Orders?$expand=Customer,Items&$orderby=Id"), OeRequestHeaders.JsonDefault, stream, CancellationToken.None);
            await parser.ExecuteGetAsync(new Uri("http://dummy/Orders"), OeRequestHeaders.JsonDefault, stream, CancellationToken.None);
            stream.Position = 0;
            String result = new StreamReader(stream).ReadToEnd();
        }

        public static DbContextOptions<DynamicDbContext> CreateOptionsSqlServer(bool useRelationalNulls)
        {
            var optionsBuilder = new DbContextOptionsBuilder<DynamicDbContext>();
            optionsBuilder = optionsBuilder.UseSqlServer(@"Server=.\sqlexpress;Initial Catalog=OdataToEntity;Trusted_Connection=Yes;", opt => opt.UseRelationalNulls(useRelationalNulls));
            return optionsBuilder.Options;
        }
        public static DbContextOptions<DynamicDbContext> CreateOptionsPostgreSql(bool useRelationalNulls)
        {
            var optionsBuilder = new DbContextOptionsBuilder<DynamicDbContext>();
            optionsBuilder.UseNpgsql(@"Host=localhost;Port=5432;Database=OdataToEntity;Pooling=true", opt => opt.UseRelationalNulls(useRelationalNulls));
            return optionsBuilder.Options;
        }

        public static InformationSchemaMapping GetMappings()
        {
            return new InformationSchemaMapping
            {
                Operations = new OperationMapping[]
                {
                    new OperationMapping("dbo.GetOrders", "dbo.Orders"),
                    new OperationMapping("dbo.TableFunction", "dbo.Orders"),
                    new OperationMapping("dbo.TableFunctionWithParameters", "dbo.Orders")
                },
                Tables = new TableMapping[]
                {
                    new TableMapping("dbo.Categories")
                    {
                        Navigations = new[]
                        {
                            new NavigationMapping("FK_Categories_Categories", "Parent")
                        }
                    },
                    new TableMapping("dbo.Customers")
                    {
                        Navigations = new []
                        {
                            new NavigationMapping("FK_Orders_AltCustomers", "AltOrders"),
                            new NavigationMapping(null, "ShippingAddresses") { ManyToManyTarget = "ShippingAddresses" }
                        }
                    },
                    new TableMapping("dbo.CustomerShippingAddress"),
                    new TableMapping("dbo.ManyColumns"),
                    new TableMapping("dbo.Orders")
                    {
                        Navigations = new []
                        {
                            new NavigationMapping("FK_OrderItem_Order", "Items"),
                            new NavigationMapping("FK_Orders_AltCustomers", "AltCustomer"),
                        }
                    },
                    new TableMapping("dbo.OrderItems"),
                    new TableMapping("dbo.ShippingAddresses"),
                    new TableMapping("dbo.OrderItemsView")
                }
            };
        }
    }
}