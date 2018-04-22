using Microsoft.OData.Edm;
using OdataToEntity.EfCore;
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
            var ethalonDataAdapter = new OrderDbDataAdapter(false, false, null);
            EdmModel ethalonEdmModel = ethalonDataAdapter.BuildEdmModel();
            String ethalonSchema = TestHelper.GetCsdlSchema(ethalonEdmModel);

            var testDataAdapter = new OrderDbDataAdapter(false, false, null);
            EdmModel testEdmModel = testDataAdapter.BuildEdmModelFromEfCoreModel();
            String testSchema = TestHelper.GetCsdlSchema(testEdmModel);

            Assert.Equal(ethalonSchema, testSchema);
        }
    }
}
