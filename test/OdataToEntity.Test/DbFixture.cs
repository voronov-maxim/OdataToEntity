using Microsoft.EntityFrameworkCore;
using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OdataToEntity.Db;
using OdataToEntity.Test.Model;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity.Test
{
    public abstract class DbFixture
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

            _edmModel = OeDataAdapter.BuildEdmModel();
        }

        public OrderContext CreateContext()
        {
            return OrderContext.Create(_databaseName);
        }
        public virtual async Task Execute<T, TResult>(QueryParametersScalar<T, TResult> parameters)
        {
            IList fromOe = await ExecuteOe<TResult>(parameters.RequestUri, false);
            IList fromDb;
            using (var dataContext = (DbContext)DbDataAdapter.CreateDataContext())
                fromDb = TestHelper.ExecuteDb(dataContext, parameters.Expression);

            Console.WriteLine(parameters.RequestUri);
            TestHelper.Compare(fromDb, fromOe, null);
        }
        public virtual async Task Execute<T, TResult>(QueryParameters<T, TResult> parameters)
        {
            IList fromOe = await ExecuteOe<TResult>(parameters.RequestUri, parameters.NavigationNextLink);
            IList fromDb;
            IReadOnlyList<IncludeVisitor.Include> includes;
            using (var dataContext = (DbContext)DbDataAdapter.CreateDataContext())
                fromDb = TestHelper.ExecuteDb(dataContext, parameters.Expression, out includes);

            Console.WriteLine(parameters.RequestUri);
            TestHelper.Compare(fromDb, fromOe, includes);
        }
        internal async Task ExecuteBatchAsync(String batchName)
        {
            var parser = new OeParser(new Uri("http://dummy/"), OeDataAdapter, EdmModel);
            String fileName = Directory.EnumerateFiles(".", batchName + ".batch", SearchOption.AllDirectories).First();
            byte[] bytes = File.ReadAllBytes(fileName);
            var responseStream = new MemoryStream();

            await parser.ExecuteBatchAsync(new MemoryStream(bytes), responseStream, CancellationToken.None);
        }
        public async Task<IList> ExecuteOe<TResult>(String requestUri, bool navigationNextLink)
        {
            var parser = new OeParser(new Uri("http://dummy/"), OeDataAdapter, EdmModel) { NavigationNextLink = navigationNextLink };
            var stream = new MemoryStream();
            await parser.ExecuteQueryAsync(ParseUri(requestUri), OeRequestHeaders.JsonDefault, stream, CancellationToken.None);
            stream.Position = 0;

            if (typeof(TResult).IsPrimitive)
            {
                TypeConverter converter = TypeDescriptor.GetConverter(typeof(TResult));
                return new Object[] { converter.ConvertFromString(new StreamReader(stream).ReadToEnd()) };
            }

            IList fromOe;
            ResponseReader responseReader;
            if (typeof(TResult) == typeof(Object))
            {
                responseReader = new OpenTypeResponseReader(EdmModel, DbDataAdapter.EntitySetMetaAdapters);
                fromOe = responseReader.Read(stream).Cast<Object>().ToList();
            }
            else
            {
                responseReader = new ResponseReader(EdmModel, DbDataAdapter.EntitySetMetaAdapters);
                fromOe = responseReader.Read<TResult>(stream).ToList();
            }

            var navigationParser = new OeParser(new Uri("http://dummy/"), DbDataAdapter, EdmModel);
            foreach (Object entity in fromOe)
            {
                await responseReader.FillNextLinkProperties(navigationParser, entity, CancellationToken.None);
                //Dictionary<PropertyInfo, ODataResourceSetBase> navigationProperties;
                //if (responseReader.NavigationPropertyEntities.TryGetValue(entity, out navigationProperties))
                //    SetNullEmptyCollection(entity, navigationProperties.Keys);
            }

            return fromOe;
        }
        public abstract void Initalize();
        public ODataUri ParseUri(String requestRelativeUri)
        {
            var baseUri = new Uri("http://dummy/");
            var odataParser = new ODataUriParser(EdmModel, baseUri, new Uri(baseUri, requestRelativeUri));
            odataParser.Resolver.EnableCaseInsensitive = true;
            return odataParser.ParseUri();
        }
        //private static void SetNullEmptyCollection(Object entity, IEnumerable<PropertyInfo> navigationProperties)
        //{
        //    return;
        //    if (entity is SortedDictionary<String, Object> openType)
        //    {
        //        foreach (PropertyInfo navigationProperty in navigationProperties)
        //            if (openType[navigationProperty.Name] is IEnumerable collection && !collection.GetEnumerator().MoveNext())
        //                openType[navigationProperty.Name] = null;
        //    }
        //    else
        //        foreach (PropertyInfo navigationProperty in navigationProperties)
        //            if (navigationProperty.GetValue(entity) is IEnumerable collection && !collection.GetEnumerator().MoveNext())
        //                navigationProperty.SetValue(entity, null);
        //}

        internal OrderDbDataAdapter DbDataAdapter => _dbDataAdapter;
        internal EdmModel EdmModel => _edmModel;
        internal OrderOeDataAdapter OeDataAdapter => _oeDataAdapter;
    }
}
