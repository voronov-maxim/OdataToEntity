using Microsoft.EntityFrameworkCore;
using Microsoft.OData;
using Microsoft.OData.Edm;
using OdataToEntity.Query;
using OdataToEntity.Test.Model;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity.Test
{
    public abstract class DbFixture
    {
        private readonly String _databaseName;
        private readonly bool _useRelationalNulls;

        protected DbFixture(bool allowCache, bool useRelationalNulls, ModelBoundTestKind modelBoundTestKind)
            : this(allowCache, useRelationalNulls, OrderContext.GenerateDatabaseName())
        {
            if (modelBoundTestKind == ModelBoundTestKind.Attribute)
                ModelBoundProvider = new Query.Builder.OeModelBoundAttributeBuilder(EdmModel).BuildProvider();
            else if (modelBoundTestKind == ModelBoundTestKind.Fluent)
                ModelBoundProvider = CreateModelBoundProvider(EdmModel);
        }
        private DbFixture(bool allowCache, bool useRelationalNulls, String databaseName)
            : this(useRelationalNulls, databaseName, new OrderDataAdapter(allowCache, useRelationalNulls, databaseName))
        {
        }
        private DbFixture(bool useRelationalNulls, String databaseName, Db.OeDataAdapter dataAdapter)
            : this(useRelationalNulls, databaseName, dataAdapter, OrderDataAdapter.CreateMetadataProvider(useRelationalNulls, databaseName))
        {
        }
        private DbFixture(bool useRelationalNulls, String databaseName, Db.OeDataAdapter dataAdapter, ModelBuilder.OeEdmModelMetadataProvider metadataProvider)
            : this(useRelationalNulls, databaseName, OrderContextOptions.BuildEdmModel(dataAdapter, metadataProvider), metadataProvider)
        {
        }
        private DbFixture(bool useRelationalNulls, String databaseName, EdmModel edmModel, ModelBuilder.OeEdmModelMetadataProvider metadataProvider)
        {
            _useRelationalNulls = useRelationalNulls;
            _databaseName = databaseName;
            EdmModel = edmModel;
            MetadataProvider = metadataProvider;
        }

        public OrderContext CreateContext()
        {
            return new OrderContext(OrderContextOptions.Create(_useRelationalNulls, _databaseName));
        }
        private static OeModelBoundProvider CreateModelBoundProvider(IEdmModel edmModel)
        {
            var modelBoundBuilder = new Query.Builder.OeModelBoundFluentBuilder(edmModel);
            modelBoundBuilder.EntitySet<Customer>("Customers").EntityType
                .Expand(SelectExpandType.Disabled, "AltOrders")
                .Expand(SelectExpandType.Automatic, "Orders")
                .Property(c => c.Orders).Count(QueryOptionSetting.Disabled);

            modelBoundBuilder.EntitySet<Order>("Orders").EntityType
                .Count(QueryOptionSetting.Allowed)
                .Expand(SelectExpandType.Automatic, "Customer", "Items")
                .Page(2, 1)
                .Select(SelectExpandType.Automatic, "Name", "Date", "Status")
                .Property(o => o.Customer).Select(SelectExpandType.Automatic, "Name", "Sex")
                .Property("Items").Count(QueryOptionSetting.Allowed).Page(2, 1).OrderBy(QueryOptionSetting.Allowed, "Id");

            modelBoundBuilder.EntitySet<OrderItem>("OrderItems").EntityType
                .Count(QueryOptionSetting.Disabled)
                .Filter(QueryOptionSetting.Disabled, "Id")
                .OrderBy(QueryOptionSetting.Disabled)
                .Select(SelectExpandType.Disabled, "Id", "OrderId");

            return modelBoundBuilder.BuildProvider();

        }
        protected static void EnsureCreated(IEdmModel edmModel)
        {
            Db.OeDataAdapter dataAdapter = edmModel.GetDataAdapter(edmModel.EntityContainer);
            var dbContext = (DbContext)dataAdapter.CreateDataContext();
            dbContext.Database.EnsureCreated();

            if (dataAdapter.EntitySetAdapters.Find(typeof(OrderItemsView)) != null)
                dbContext.Database.ExecuteSqlCommand(
                    @"create view OrderItemsView(Name, Product) as select o.Name, i.Product from Orders o inner join OrderItems i on o.Id = i.OrderId");

            dataAdapter.CloseDataContext(dbContext);

            foreach (IEdmModel refModel in edmModel.ReferencedModels)
                if (refModel.EntityContainer != null && refModel is EdmModel)
                    EnsureCreated(refModel);
        }
        public virtual async Task Execute<T, TResult>(QueryParametersScalar<T, TResult> parameters)
        {
            IList fromOe = await ExecuteOe<TResult>(parameters.RequestUri, false, 0).ConfigureAwait(false);

            ODataUri odataUri = ParseUri(parameters.RequestUri);
            IEdmModel edmModel = EdmModel.GetEdmModel(odataUri.Path);
            Db.OeDataAdapter oeDataAdapter = edmModel.GetDataAdapter(edmModel.EntityContainer);
            Db.OeDataAdapter dbDataAdapter = ((ITestDbDataAdapter)oeDataAdapter).DbDataAdapter;

            IList fromDb;
            using (var dataContext = (DbContext)dbDataAdapter.CreateDataContext())
                fromDb = TestHelper.ExecuteDb(dbDataAdapter.EntitySetAdapters, dataContext, parameters.Expression);

            Console.WriteLine(parameters.RequestUri);
            TestHelper.Compare(fromDb, fromOe, null);
        }
        public virtual async Task Execute<T, TResult>(QueryParameters<T, TResult> parameters)
        {
            IList fromOe = await ExecuteOe<TResult>(parameters.RequestUri, parameters.NavigationNextLink, parameters.PageSize).ConfigureAwait(false);

            ODataUri odataUri = ParseUri(parameters.RequestUri);
            IEdmModel edmModel = EdmModel.GetEdmModel(odataUri.Path);
            Db.OeDataAdapter oeDataAdapter = edmModel.GetDataAdapter(edmModel.EntityContainer);
            Db.OeDataAdapter dbDataAdapter = ((ITestDbDataAdapter)oeDataAdapter).DbDataAdapter;

            IList fromDb;
            IReadOnlyList<EfInclude> includes;
            using (var dataContext = (DbContext)dbDataAdapter.CreateDataContext())
                fromDb = TestHelper.ExecuteDb(dbDataAdapter, dataContext, parameters.Expression, out includes);

            Console.WriteLine(parameters.RequestUri);
            TestHelper.Compare(fromDb, fromOe, includes);
        }
        internal async Task ExecuteBatchAsync(String batchName)
        {
            var parser = new OeParser(new Uri("http://dummy/"), EdmModel);
            String fileName = Directory.EnumerateFiles(".", batchName + ".batch", SearchOption.AllDirectories).First();
            byte[] bytes = File.ReadAllBytes(fileName);
            var responseStream = new MemoryStream();

            await parser.ExecuteBatchAsync(new MemoryStream(bytes), responseStream, CancellationToken.None).ConfigureAwait(false);
        }
        public async Task<IList> ExecuteOe<TResult>(String requestUri, bool navigationNextLink, int pageSize)
        {
            ODataUri odataUri = ParseUri(requestUri);
            var parser = new OeParser(odataUri.ServiceRoot, EdmModel, ModelBoundProvider);
            var uri = new Uri(odataUri.ServiceRoot, requestUri);
            OeRequestHeaders requestHeaders = OeRequestHeaders.JsonDefault.SetMaxPageSize(pageSize).SetNavigationNextLink(navigationNextLink);

            long count = -1;
            var fromOe = new List<Object>();
            do
            {
                var response = new MemoryStream();
                await parser.ExecuteGetAsync(uri, requestHeaders, response, CancellationToken.None).ConfigureAwait(false);
                response.Position = 0;

                List<Object> result;
                ResponseReader responseReader;
                if (typeof(TResult).IsPrimitive)
                {
                    TypeConverter converter = TypeDescriptor.GetConverter(typeof(TResult));
                    return new Object[] { converter.ConvertFromString(new StreamReader(response).ReadToEnd()) };
                }
                else if (typeof(TResult) == typeof(Object) && (requestUri.Contains("$apply=") || requestUri.Contains("$compute=")))
                {
                    responseReader = new OpenTypeResponseReader(EdmModel.GetEdmModel(odataUri.Path));
                    result = responseReader.Read(response).Cast<Object>().ToList();
                }
                else
                {
                    responseReader = new ResponseReader(EdmModel.GetEdmModel(odataUri.Path));
                    result = responseReader.Read(response).Cast<Object>().ToList();
                }

                if (pageSize > 0)
                    Xunit.Assert.InRange(result.Count, 0, requestHeaders.MaxPageSize);
                fromOe.AddRange(result);

                await responseReader.FillNextLinkProperties(parser, CancellationToken.None).ConfigureAwait(false);

                if (count < 0)
                    count = responseReader.ResourceSet.Count.GetValueOrDefault();

                uri = responseReader.ResourceSet.NextPageLink;
            }
            while (uri != null);

            if (odataUri.QueryCount != null)
                Xunit.Assert.Equal(count, fromOe.Count);

            return fromOe;
        }
        public abstract Task Initalize();
        public ODataUri ParseUri(String requestRelativeUri)
        {
            var baseUri = new Uri("http://dummy/");
            return OeParser.ParseUri(EdmModel, baseUri, new Uri(baseUri, requestRelativeUri));
        }

        public EdmModel EdmModel { get; }
        public ModelBuilder.OeEdmModelMetadataProvider MetadataProvider { get; }
        public Query.OeModelBoundProvider ModelBoundProvider { get; }
    }
}
