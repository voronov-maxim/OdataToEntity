using GraphQL;
using GraphQL.Http;
using GraphQL.Types;
using Microsoft.OData.Edm;
using Microsoft.OData.Edm.Csdl;
using OdataToEntity.EfCore;
using OdataToEntity.GraphQL;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace OdataToEntity.Test.GraphQL
{
    public abstract class DbFixture
    {
        protected DbFixture(Db.OeDataAdapter dataAdapter)
        {
            DataAdapter = dataAdapter;
            EdmModel = dataAdapter.BuildEdmModelFromEfCoreModel();
            Schema = new OeSchemaBuilder(dataAdapter, EdmModel).Build();
        }

        public Task<String> Execute(String query)
        {
            return Execute(query, null);
        }
        public async Task<String> Execute(String query, Inputs inputs)
        {
            var parser = new OeGraphqlParser(DataAdapter, EdmModel);
            return new DocumentWriter(true).Write(await parser.Execute(query, inputs));
        }
        public static String GetCsdlSchema(IEdmModel edmModel)
        {
            using (var stream = new MemoryStream())
            {
                using (XmlWriter xmlWriter = XmlWriter.Create(stream, new XmlWriterSettings() { Indent = true }))
                {
                    if (!CsdlWriter.TryWriteCsdl(edmModel, xmlWriter, CsdlTarget.OData, out _))
                        return null;
                }

                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        protected Db.OeDataAdapter DataAdapter { get; }
        protected IEdmModel EdmModel { get; }
        protected Schema Schema { get; }
    }
}
