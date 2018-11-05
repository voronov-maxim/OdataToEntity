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
            IEdmModel edmModel = dataAdapter.BuildEdmModelFromEfCoreModel();
            var schemaBuilder = new OeSchemaBuilder(dataAdapter, edmModel, new ModelBuilder.OeEdmModelMetadataProvider());

            DataAdapter = dataAdapter;
            Schema = schemaBuilder.Build();
        }

        public Task<String> Execute(String query)
        {
            return Execute(query, null);
        }
        public async Task<string> Execute(String query, Inputs inputs)
        {
            Object dataContext = DataAdapter.CreateDataContext();

            var result = await new DocumentExecuter().ExecuteAsync(options =>
            {
                options.Inputs = inputs;
                options.Query = query;
                options.Schema = Schema;
                options.UserContext = dataContext;
            }).ConfigureAwait(false);

            DataAdapter.CloseDataContext(dataContext);

            return new DocumentWriter(true).Write(result);
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
        protected Schema Schema { get; }
    }
}
