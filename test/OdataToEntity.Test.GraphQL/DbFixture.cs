using GraphQL;
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
    public abstract class DbFixture : IDisposable
    {
        private readonly OeGraphqlParser _graphqlParser;

        protected DbFixture(Db.OeDataAdapter dataAdapter1, Db.OeDataAdapter dataAdapter2)
        {
            EdmModel refModel = dataAdapter2.BuildEdmModelFromEfCoreModel();
            EdmModel = dataAdapter1.BuildEdmModelFromEfCoreModel(refModel);

            Schema = new OeSchemaBuilder(EdmModel).Build();
            _graphqlParser = new OeGraphqlParser(EdmModel);
        }

        public void Dispose()
        {
            Schema.Dispose();
        }
        public Task<String> Execute(String query)
        {
            return Execute(query, null);
        }
        public async Task<String> Execute(String query, Inputs inputs)
        {
            return await (await _graphqlParser.Execute(query, inputs)).ToStringAsync();
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

        protected EdmModel EdmModel { get; }
        protected Schema Schema { get; }
    }
}
