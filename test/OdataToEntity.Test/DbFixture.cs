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
        protected DbFixture(IEdmModel edmModel, ModelBoundTestKind modelBoundTestKind, bool useRelationalNulls)
        {
            OeEdmModel = edmModel;

            if (modelBoundTestKind == ModelBoundTestKind.Attribute)
                ModelBoundProvider = new Query.Builder.OeModelBoundAttributeBuilder(edmModel).BuildProvider();
            else if (modelBoundTestKind == ModelBoundTestKind.Fluent)
                ModelBoundProvider = CreateModelBoundProvider(edmModel);

            DbEdmModel = OrderContextOptions.BuildDbEdmModel(useRelationalNulls);
        }

        public abstract OrderContext CreateContext();
        private static OeModelBoundProvider CreateModelBoundProvider(IEdmModel edmModel)
        {
            var modelBoundBuilder = new Query.Builder.OeModelBoundFluentBuilder(edmModel);
            modelBoundBuilder.EntitySet<Customer>("Customers").EntityType
                .Expand(SelectExpandType.Disabled, "AltOrders")
                .Expand(SelectExpandType.Automatic, "Orders")
                .Property(c => c.Orders).Count(QueryOptionSetting.Disabled).Property(c => c.Id);

            modelBoundBuilder.EntitySet<Order>("Orders").EntityType
                .Count(QueryOptionSetting.Allowed)
                .Expand(SelectExpandType.Automatic, "Customer", "Items")
                .Page(2, 1)
                .Select(SelectExpandType.Automatic, "Name", "Date", "Status")
                .Property(o => o.Customer).Select(SelectExpandType.Automatic, "Name", "Sex")
                .Property(o => o.Items).Count(QueryOptionSetting.Allowed).Page(2, 1).OrderBy(QueryOptionSetting.Allowed, "Id");

            modelBoundBuilder.EntitySet<OrderItem>("OrderItems").EntityType
                .Count(QueryOptionSetting.Disabled)
                .Filter(QueryOptionSetting.Disabled, "Id")
                .OrderBy(QueryOptionSetting.Disabled)
                .Select(SelectExpandType.Disabled, "Id", "OrderId");

            modelBoundBuilder.EntitySet<Category>("Categories").EntityType
                .Property(c => c.Children).NavigationNextLink();

            return modelBoundBuilder.BuildProvider();

        }
        public OeParser CreateParser(String request, OeModelBoundProvider modelBoundProvider)
        {
            ODataUri odataUri = ParseUri(request);
            IEdmModel edmModel = OeEdmModel.GetEdmModel(odataUri.Path);
            return new OeParser(odataUri.ServiceRoot, edmModel, modelBoundProvider, ServiceProvider);
        }
        public virtual async Task Execute<T, TResult>(QueryParametersScalar<T, TResult> parameters)
        {
            IList fromOe = await ExecuteOe<TResult>(parameters.RequestUri, false, 0).ConfigureAwait(false);

            ODataUri odataUri = ParseUri(parameters.RequestUri);
            IEdmModel dbEdmModel = TestHelper.GetEdmModel(DbEdmModel, odataUri.Path);
            Db.OeDataAdapter dbDataAdapter = DbEdmModel.GetDataAdapter(dbEdmModel.EntityContainer);

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
            IEdmModel dbEdmModel = TestHelper.GetEdmModel(DbEdmModel, odataUri.Path);
            Db.OeDataAdapter dbDataAdapter = DbEdmModel.GetDataAdapter(dbEdmModel.EntityContainer);

            IList fromDb;
            IReadOnlyList<EfInclude> includes;
            using (var dataContext = (DbContext)dbDataAdapter.CreateDataContext())
                fromDb = TestHelper.ExecuteDb(dbDataAdapter, dataContext, parameters.Expression, out includes);

            Console.WriteLine(parameters.RequestUri);
            TestHelper.Compare(fromDb, fromOe, includes);
        }
        internal static async Task ExecuteBatchAsync(IEdmModel edmModel, String batchName, IServiceProvider serviceProvider = null)
        {
            var parser = new OeParser(new Uri("http://dummy/"), edmModel, null, serviceProvider);
            String fileName = Directory.EnumerateFiles(".", batchName + ".batch", SearchOption.AllDirectories).First();
            byte[] bytes = File.ReadAllBytes(fileName);
            var responseStream = new MemoryStream();

            await parser.ExecuteBatchAsync(new MemoryStream(bytes), responseStream, CancellationToken.None).ConfigureAwait(false);
        }
        public async Task<IList> ExecuteOe<TResult>(String requestUri, bool navigationNextLink, int pageSize)
        {
            OeModelBoundProvider modelBoundProvider = ModelBoundProvider;
            if (modelBoundProvider == null)
            {
                var modelBoundProviderBuilder = new PageNextLinkModelBoundBuilder(OeEdmModel, IsSqlite);
                modelBoundProvider = modelBoundProviderBuilder.BuildProvider(pageSize, navigationNextLink);
            }

            OeParser parser = CreateParser(requestUri, modelBoundProvider);
            var uri = new Uri(parser.BaseUri, requestUri);
            OeRequestHeaders requestHeaders = OeRequestHeaders.JsonDefault.SetMaxPageSize(pageSize);

            long count = -1;
            ODataUri odataUri;
            var fromOe = new List<Object>();
            do
            {
                odataUri = ParseUri(uri.OriginalString);
                var response = new MemoryStream();
                await parser.ExecuteQueryAsync(odataUri, requestHeaders, response, CancellationToken.None).ConfigureAwait(false);
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
                    responseReader = new OpenTypeResponseReader(TestHelper.GetEdmModel(DbEdmModel, odataUri.Path), ServiceProvider);
                    result = responseReader.Read(response).Cast<Object>().ToList();
                }
                else
                {
                    responseReader = new ResponseReader(TestHelper.GetEdmModel(DbEdmModel, odataUri.Path), ServiceProvider);
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
        public virtual ODataUri ParseUri(String requestUri)
        {
            var baseUri = new Uri("http://dummy/");
            if (requestUri.StartsWith(baseUri.OriginalString))
                return OeParser.ParseUri(OeEdmModel, baseUri, new Uri(requestUri));
            else
                return OeParser.ParseUri(OeEdmModel, baseUri, new Uri(baseUri, requestUri));
        }

        protected IEdmModel DbEdmModel { get; }
        protected internal virtual bool IsSqlite => false;
        public OeModelBoundProvider ModelBoundProvider { get; }
        public IEdmModel OeEdmModel { get; }
        protected virtual IServiceProvider ServiceProvider => null;
    }
}
