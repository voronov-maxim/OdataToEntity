extern alias lq2db;

using Microsoft.OData.Edm;
using OdataToEntity.EfCore;
using OdataToEntity.Linq2Db;
using OdataToEntity.Test.Model;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Xunit;

using OdataToEntityDB = lq2db::OdataToEntity.Test.Model.OdataToEntityDB;

namespace OdataToEntity.Test
{
    public class EdmModelBuilderTest
    {
        private static String FixNamesInSchema(String schema)
        {
            Type efCore = typeof(OrderContext);
            Type linq2db = typeof(OdataToEntityDB);

            XDocument xdoc = XDocument.Parse(schema);
            XElement xschema = xdoc.Root.Descendants().Where(x => x.Name.LocalName == "Schema").Last();
            foreach (XElement xelement in xschema.Elements())
            {
                XAttribute xattribute = xelement.Attribute("Name");
                xattribute.SetValue(xattribute.Value.Replace(linq2db.Name, efCore.Name));
            }

            using (var stream = new MemoryStream())
            using (var xwriter = XmlWriter.Create(stream, new XmlWriterSettings() { Indent = true, Encoding = new UTF8Encoding(false) }))
            {
                xdoc.WriteTo(xwriter);
                xwriter.Flush();
                String fixSchema = Encoding.UTF8.GetString(stream.ToArray());
                return fixSchema.Replace(linq2db.Namespace, efCore.Namespace);
            }
        }
        [Fact]
        public void FluentApi()
        {
            var ethalonDataAdapter = new OeEfCoreDataAdapter<OrderContext>();
            EdmModel ethalonEdmModel = ethalonDataAdapter.BuildEdmModel();
            String ethalonSchema = TestHelper.GetCsdlSchema(ethalonEdmModel);
            if (ethalonSchema == null)
                throw new InvalidOperationException("Invalid ethalon schema");

            var testDataAdapter = new OrderDataAdapter(false, false, null);
            EdmModel testEdmModel = testDataAdapter.BuildEdmModelFromLinq2DbModel();
            String testSchema = TestHelper.GetCsdlSchema(testEdmModel);
            if (testSchema == null)
                throw new InvalidOperationException("Invalid test schema");

            String fixTestSchema = FixNamesInSchema(testSchema);
            Assert.Equal(ethalonSchema, fixTestSchema);
        }
    }
}
