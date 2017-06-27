using Microsoft.OData.Edm;
using OdataToEntity.Db;
using OdataToEntity.Ef6;
using OdataToEntity.EfCore;
using OdataToEntity.Test.Ef6.SqlServer;
using OdataToEntity.Test.Model;
using System;
using Xunit;

namespace OdataToEntity.Test
{
    public class EdmModelBuilderTest
    {
        [Fact]
        public void FluentApi()
        {
            var ethalonDataAdapter = new OeEfCoreDataAdapter<OrderContext>();
            EdmModel ethalonEdmModel = ethalonDataAdapter.BuildEdmModel();
            String ethalonSchema = TestHelper.GetCsdlSchema(ethalonEdmModel);

            var testDataAdapter = new OeEf6DataAdapter<OrderEf6Context>();
            EdmModel testEdmModel = testDataAdapter.BuildEdmModelFromEf6Model();
            String testSchema = TestHelper.GetCsdlSchema(testEdmModel);

            Assert.Equal(ethalonSchema, testSchema);
        }
    }
}
