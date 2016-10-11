using Microsoft.OData.Edm;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OdataToEntity.Parsers;
using OdataToEntity.Test.Model;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity.Test
{
    internal sealed class DbFixture
    {
        private sealed class QueryVisitor<T> : ExpressionVisitor
        {
            private readonly IQueryable _query;
            private ConstantExpression _parameter;

            public QueryVisitor(IQueryable query)
            {
                _query = query;
            }

            protected override Expression VisitParameter(ParameterExpression node)
            {
                if (_parameter == null && node.Type == typeof(IQueryable<T>))
                {
                    _parameter = Expression.Constant(_query);
                    return _parameter;
                }
                return base.VisitParameter(node);
            }
        }

        private readonly OrderDataAdapter _dataAdapter;
        private readonly String _databaseName;
        private readonly EdmModel _edmModel;

        public DbFixture(bool clear = false)
        {
            _databaseName = OrderContext.GenerateDatabaseName();

            _dataAdapter = new OrderDataAdapter(_databaseName);
            _edmModel = new ModelBuilder.OeEdmModelBuilder(DataAdapter.EntitySetMetaAdapters.ToDictionary()).BuildEdmModel();

            if (!clear)
                ExecuteBatchAsync("Add").Wait();
        }

        public OrderContext CreateContext()
        {
            return OrderContext.Create(_databaseName);
        }
        public async Task Execute<T, TResult>(QueryParametersScalar<T, TResult> parameters)
        {
            IList fromOe = await ExecuteOe<TResult>(parameters.RequestUri);
            IList fromDb = ExecuteDb(parameters.Expression);

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
            IList fromOe = await ExecuteOe<TResult>(parameters.RequestUri);
            IList fromDb = ExecuteDb(parameters.Expression);

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
            var parser = new OeParser(new Uri("http://dummy/"), DataAdapter, EdmModel);
            byte[] bytes = File.ReadAllBytes($"Batches\\{batchName}.batch");
            var responseStream = new MemoryStream();
            await parser.ExecuteBatchAsync(new MemoryStream(bytes), responseStream, CancellationToken.None);
        }
        private IList ExecuteDb<T, TResult>(Expression<Func<IQueryable<T>, TResult>> expression)
        {
            Object dataContext = null;
            try
            {
                dataContext = DataAdapter.CreateDataContext();
                var query = (IQueryable<T>)DataAdapter.EntitySetMetaAdapters.FindByClrType(typeof(T)).GetEntitySet(dataContext);
                var visitor = new QueryVisitor<T>(query);
                Expression call = visitor.Visit(expression.Body);
                return new[] { query.Provider.Execute<TResult>(call).ToString() };
            }
            finally
            {
                if (dataContext != null)
                    DataAdapter.CloseDataContext(dataContext);
            }
        }
        private IList ExecuteDb<T, TResult>(Expression<Func<IQueryable<T>, IQueryable<TResult>>> expression)
        {
            Object dataContext = null;
            try
            {
                dataContext = DataAdapter.CreateDataContext();
                var query = (IQueryable<T>)DataAdapter.EntitySetMetaAdapters.FindByClrType(typeof(T)).GetEntitySet(dataContext);
                var visitor = new QueryVisitor<T>(query);
                Expression call = visitor.Visit(expression.Body);

                IList fromDb = query.Provider.CreateQuery<TResult>(call).ToList();
                if (typeof(TResult) == typeof(Object))
                    fromDb = SortProperty(fromDb);
                return fromDb;
            }
            finally
            {
                if (dataContext != null)
                    DataAdapter.CloseDataContext(dataContext);
            }
        }
        private async Task<IList> ExecuteOe<TResult>(String requestUri)
        {
            var accept = "application/json;odata.metadata=minimal";
            var parser = new OeParser(new Uri("http://dummy/"), DataAdapter, EdmModel);
            OeRequestHeaders headers = OeRequestHeaders.Parse(accept);
            var stream = new MemoryStream();
            await parser.ExecuteQueryAsync(new Uri("http://dummy/" + requestUri), headers, stream, CancellationToken.None);
            stream.Position = 0;

            var reader = new OeResponseReader(EdmModel, DataAdapter.EntitySetMetaAdapters);
            IList fromOe;
            if (typeof(TResult) == typeof(Object))
                fromOe = reader.ReadOpenType(stream).Select(t => JRawToEnum(t)).ToList();
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
        private static IList SortProperty(IList items)
        {
            var jobjects = new List<JObject>(items.Count);
            foreach (Object item in items)
            {
                var jobject = new JObject();
                foreach (PropertyInfo property in item.GetType().GetProperties().OrderBy(p => p.Name))
                {
                    Object value = property.GetValue(item);
                    jobject.Add(property.Name, new JValue(value));
                }
                jobjects.Add(jobject);
            }
            return jobjects;
        }

        internal OrderDataAdapter DataAdapter => _dataAdapter;
        internal EdmModel EdmModel => _edmModel;
    }
}
