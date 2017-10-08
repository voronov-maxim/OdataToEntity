using Microsoft.OData.Edm;
using OdataToEntity.Db;
using OdataToEntity.Ef6;
using OdataToEntity.EfCore;
using OdataToEntity.ModelBuilder;
using OdataToEntity.Test.Ef6.SqlServer;
using OdataToEntity.Test.Model;
using System;
using System.Data.Entity;
using System.Linq;
using Xunit;

namespace OdataToEntity.Test
{
    public class EdmModelBuilderTest
    {
        private static EdmModel BuildEdmModelFromEf6Model(OeDataAdapter dataAdapter)
        {
            using (var context = (DbContext)dataAdapter.CreateDataContext())
            {
                var metadataProvider = new OeEf6EdmModelMetadataProvider(context);
                var modelBuilder = new OeEdmModelBuilder(metadataProvider, dataAdapter.EntitySetMetaAdapters.ToDictionary());
                FixOperations(dataAdapter, modelBuilder);
                return modelBuilder.BuildEdmModel();
            }
        }
        private static void FixOperations(OeDataAdapter dataAdapter, OeEdmModelBuilder modelBuilder)
        {
            OeOperationConfiguration[] operations = dataAdapter.OperationAdapter.GetOperations();
            if (operations != null)
                foreach (OeOperationConfiguration operation in operations)
                {
                    String methodInfoName = nameof(OrderContext) + "." + operation.MethodInfoName.Split('.').Last();
                    var fixOperation = new OeOperationConfiguration(operation.Name, typeof(OrderContext).Namespace, methodInfoName, operation.ReturnType, operation.IsDbFunction);
                    foreach (OeOperationParameterConfiguration parameter in operation.Parameters)
                        fixOperation.AddParameter(parameter.Name, parameter.ClrType);
                    modelBuilder.AddOperation(fixOperation);
                }
        }
        [Fact]
        public void FluentApi()
        {
            var ethalonDataAdapter = new OeEfCoreDataAdapter<OrderContext>();
            EdmModel ethalonEdmModel = ethalonDataAdapter.BuildEdmModel();
            String ethalonSchema = TestHelper.GetCsdlSchema(ethalonEdmModel);

            var testDataAdapter = new OeEf6DataAdapter<OrderEf6Context>();
            EdmModel testEdmModel = BuildEdmModelFromEf6Model(testDataAdapter);
            String testSchema = TestHelper.GetCsdlSchema(testEdmModel);

            Assert.Equal(ethalonSchema, testSchema);
        }
    }
}
