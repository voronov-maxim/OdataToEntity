using Microsoft.OData.Edm;
using OdataToEntity.EfCore.DynamicDataContext;
using OdataToEntity.EfCore.DynamicDataContext.InformationSchema;
using OdataToEntity.Test;
using System;
using System.Diagnostics;
using System.IO;
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
        static async Task Main(String[] args)
        {
            //PerformanceCacheTest.RunTest(100);
            new PLNull(new PLNull_DbFixtureInitDb()).FilterDateTimeOffsetYearMonthDay(0).GetAwaiter().GetResult();
            //new ProcedureTest().ScalarFunction_get().GetAwaiter().GetResult();
            //new PLNull_ManyColumns(new PLNull_ManyColumnsFixtureInitDb()).Filter(1).GetAwaiter().GetResult();

            InformationSchemaMapping informationSchemaMapping = GetMappings();
            ProviderSpecificSchema providerSchema = CreateSchemaSqlServer(true);

            EdmModel dynamicEdmModel;
            using (var metadataProvider = providerSchema.CreateMetadataProvider(informationSchemaMapping))
            {
                DynamicTypeDefinitionManager typeDefinitionManager = DynamicTypeDefinitionManager.Create(metadataProvider);
                var dataAdapter = new DynamicDataAdapter(typeDefinitionManager);
                dynamicEdmModel = dataAdapter.BuildEdmModel(metadataProvider);
            }
            String csdlSchema = TestHelper.GetCsdlSchema(dynamicEdmModel);

            var parser = new OeParser(new Uri("http://dummy"), dynamicEdmModel);
            var stream = new MemoryStream();
            await parser.ExecuteGetAsync(new Uri("http://dummy/Orders?$expand=Customer,Items&$orderby=Id"), OeRequestHeaders.JsonDefault, stream, CancellationToken.None);
            stream.Position = 0;
            String result = new StreamReader(stream).ReadToEnd();
        }

        public static ProviderSpecificSchema CreateSchemaSqlServer(bool useRelationalNulls)
        {
            var optionsFactory = new DynamicSchemaFactory("sqlserver", @"Server=.\sqlexpress;Initial Catalog=OdataToEntity;Trusted_Connection=Yes;");
            return optionsFactory.CreateSchema(useRelationalNulls);
        }
        public static ProviderSpecificSchema CreateSchemaPostgreSql(bool useRelationalNulls)
        {
            var optionsFactory = new DynamicSchemaFactory("postgresql", "Host=localhost;Port=5432;Database=OdataToEntity;Pooling=true");
            return optionsFactory.CreateSchema(useRelationalNulls);
        }
        public static ProviderSpecificSchema CreateSchemaMySql(bool useRelationalNulls)
        {
            var optionsFactory = new DynamicSchemaFactory("mysql", "server=localhost;database=dbo;user=root;password=123456");
            return optionsFactory.CreateSchema(useRelationalNulls);
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
                    new TableMapping("dbo.Categories", "Categories"),
                    new TableMapping("dbo.Customers", "Customers")
                    {
                        Navigations = new []
                        {
                            new NavigationMapping(null, "AltOrders") { ConstraintName = "FK_Orders_AltCustomers" },
                            new NavigationMapping(null, "ShippingAddresses") { ManyToManyTarget = "ShippingAddresses" }
                        }
                    },
                    new TableMapping("dbo.CustomerShippingAddress", "CustomerShippingAddress"),
                    new TableMapping("dbo.ManyColumns", "ManyColumns"),
                    new TableMapping("dbo.Orders", "Orders")
                    {
                        Navigations = new []
                        {
                            new NavigationMapping("dbo.OrderItems", "Items"),
                            new NavigationMapping(null, "AltCustomer") { ConstraintName = "FK_Orders_AltCustomers" },
                        }
                    },
                    new TableMapping("dbo.OrderItems", "OrderItems"),
                    new TableMapping("dbo.ShippingAddresses", "ShippingAddresses"),
                    new TableMapping("dbo.OrderItemsView", "OrderItemsView")
                }
            };
        }
    }
}