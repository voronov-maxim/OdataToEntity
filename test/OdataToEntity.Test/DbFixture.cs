using Microsoft.EntityFrameworkCore;
using Microsoft.OData.Edm;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OdataToEntity.Test.Model;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity.Test
{
    public class DbFixture
    {
        private readonly OrderDbDataAdapter _dbDataAdapter;
        private readonly String _databaseName;
        private readonly EdmModel _edmModel;
        private readonly OrderOeDataAdapter _oeDataAdapter;

        public DbFixture()
        {
            _databaseName = OrderContext.GenerateDatabaseName();
            _dbDataAdapter = new OrderDbDataAdapter(_databaseName);
            _oeDataAdapter = new OrderOeDataAdapter(_databaseName);

            Db.OeEntitySetMetaAdapterCollection metaAdapters = OeDataAdapter.EntitySetMetaAdapters;
            var modelBuilder = new ModelBuilder.OeEdmModelBuilder(metaAdapters.EdmModelMetadataProvider, metaAdapters.ToDictionary());
            _edmModel = modelBuilder.BuildEdmModel();
        }

        public OrderContext CreateContext()
        {
            return OrderContext.Create(_databaseName);
        }
        public async Task Execute<T, TResult>(QueryParametersScalar<T, TResult> parameters)
        {
            IList fromOe = await ExecuteOe<TResult>(parameters.RequestUri, typeof(T));
            IList fromDb;
            using (var dataContext = (DbContext)DbDataAdapter.CreateDataContext())
                fromDb = TestHelper.ExecuteDb(dataContext, parameters.Expression);

            var settings = new JsonSerializerSettings()
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                NullValueHandling = NullValueHandling.Ignore
            };
            String jsonOe = JsonConvert.SerializeObject(fromOe, settings);
            String jsonDb = JsonConvert.SerializeObject(fromDb, settings);

            Console.WriteLine(parameters.RequestUri);
            Xunit.Assert.Equal(jsonDb, jsonOe);
        }
        public async Task Execute<T, TResult>(QueryParameters<T, TResult> parameters)
        {
            IList fromOe = await ExecuteOe<TResult>(parameters.RequestUri, typeof(T));
            IList fromDb;
            using (var dataContext = (DbContext)DbDataAdapter.CreateDataContext())
                fromDb = TestHelper.ExecuteDb(dataContext, parameters.Expression);

            var settings = new JsonSerializerSettings()
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                NullValueHandling = NullValueHandling.Ignore
            };
            String jsonOe = JsonConvert.SerializeObject(fromOe, settings);
            String jsonDb = JsonConvert.SerializeObject(fromDb, settings);

            Console.WriteLine(parameters.RequestUri);
            Xunit.Assert.Equal(jsonDb, jsonOe);
        }
        internal async Task ExecuteBatchAsync(String batchName)
        {
            var parser = new OeParser(new Uri("http://dummy/"), OeDataAdapter, EdmModel);
            byte[] bytes = File.ReadAllBytes($"Batches\\{batchName}.batch");
            var responseStream = new MemoryStream();
            await parser.ExecuteBatchAsync(new MemoryStream(bytes), responseStream, CancellationToken.None);
        }
        private async Task<IList> ExecuteOe<TResult>(String requestUri, Type baseEntityType)
        {
            var accept = "application/json;odata.metadata=minimal";
            var parser = new OeParser(new Uri("http://dummy/"), OeDataAdapter, EdmModel);
            OeRequestHeaders headers = OeRequestHeaders.Parse(accept);
            var stream = new MemoryStream();
            await parser.ExecuteQueryAsync(new Uri("http://dummy/" + requestUri), headers, stream, CancellationToken.None);
            stream.Position = 0;

            var reader = new ResponseReader(EdmModel, DbDataAdapter.EntitySetMetaAdapters);
            IList fromOe;
            if (typeof(TResult) == typeof(Object))
            {
                IEnumerable<JObject> jobjects = reader.ReadOpenType(stream, baseEntityType).Select(t => JRawToEnum(t)).ToList();
                fromOe = TestHelper.SortProperty(jobjects);
            }
            else if (typeof(TResult).GetTypeInfo().IsPrimitive)
                fromOe = new String[] { new StreamReader(stream).ReadToEnd() };
            else
                fromOe = reader.ReadFeed<TResult>(stream).ToList();

            return fromOe;
        }
        private static JObject JRawToEnum(JObject jobject)
        {
            foreach (JProperty jproperty in jobject.Properties())
            {
                var jraw = jproperty.Value as JRaw;
                if (jraw != null)
                {
                    var enumValue = jraw.Value as Microsoft.OData.ODataEnumValue;
                    Type enumType = Type.GetType(enumValue.TypeName);
                    jproperty.Value = new JValue(Enum.Parse(enumType, enumValue.Value));
                }
            }
            return jobject;
        }

        internal OrderDbDataAdapter DbDataAdapter => _dbDataAdapter;
        internal EdmModel EdmModel => _edmModel;
        internal OrderOeDataAdapter OeDataAdapter => _oeDataAdapter;
    }
}
