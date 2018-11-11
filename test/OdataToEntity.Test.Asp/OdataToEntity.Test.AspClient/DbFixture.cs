using Microsoft.OData.Client;
using Microsoft.OData.Edm;
using ODataClient.Default;
using OdataToEntity.Test.Model;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace OdataToEntity.Test
{
    public class DbFixtureInitDb
    {
        private delegate IList ExecuteQueryFunc<out T>(IQueryable query, Expression expression);
        private delegate Task<IList> ExecuteQueryFuncAsync<out T>(IQueryable query, Expression expression, IReadOnlyList<LambdaExpression> navigationPropertyAccessors);

        private readonly bool _clear;

        public DbFixtureInitDb(bool clear = false)
        {
            _clear = clear;

            DataAdapter = new EfCore.OeEfCoreDataAdapter<OrderContext>();
            EdmModel = DataAdapter.BuildEdmModel();
        }

        public static Container CreateContainer(int maxPageSize)
        {
            Container container = ContainerFactory();
            if (maxPageSize > 0)
                container.BuildingRequest += (s, e) => { e.Headers.Add("Prefer", "odata.maxpagesize=" + maxPageSize.ToString(CultureInfo.InvariantCulture)); };
            return container;
        }
        private static ExecuteQueryFunc<Object> CreateDelegate(Type elementType, ExecuteQueryFunc<Object> execFunc)
        {
            MethodInfo execMethodInfo = execFunc.GetMethodInfo().GetGenericMethodDefinition().MakeGenericMethod(elementType);
            var execFuncType = execFunc.GetType().GetGenericTypeDefinition().MakeGenericType(elementType);
            return (ExecuteQueryFunc<Object>)execMethodInfo.CreateDelegate(execFuncType);
        }
        private static ExecuteQueryFuncAsync<Object> CreateDelegate(Type elementType, ExecuteQueryFuncAsync<Object> execFunc)
        {
            MethodInfo execMethodInfo = execFunc.GetMethodInfo().GetGenericMethodDefinition().MakeGenericMethod(elementType);
            var execFuncType = execFunc.GetType().GetGenericTypeDefinition().MakeGenericType(elementType);
            return (ExecuteQueryFuncAsync<Object>)execMethodInfo.CreateDelegate(execFuncType);
        }
        public async virtual Task Execute<T, TResult>(QueryParametersScalar<T, TResult> parameters)
        {
            IList fromOe = await ExecuteOe<T, TResult>(parameters.Expression, 0);
            IList fromDb;

            using (var dataContext = new OrderContext(OrderContextOptions.Create(true, null)))
                fromDb = TestHelper.ExecuteDb(DataAdapter.EntitySetAdapters, dataContext, parameters.Expression);

            TestHelper.Compare(fromDb, fromOe, DataAdapter.EntitySetAdapters.EdmModelMetadataProvider, null);
        }
        public async virtual Task Execute<T, TResult>(QueryParameters<T, TResult> parameters)
        {
            IList fromOe;
            try
            {
                fromOe = await ExecuteOe<T, TResult>(parameters.Expression, parameters.PageSize);
            }
            catch (NotSupportedException e)
            {
                if (parameters.RequestUri.Contains("$apply="))
                    throw;

                fromOe = await ExecuteOeViaHttpClient(parameters);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(e.Message);
                Console.ResetColor();
            }

            List<IncludeVisitor.Include> includes = GetIncludes(DataAdapter, parameters.Expression);
            if (typeof(TResult) == typeof(Object))
                fromOe = TestHelper.ToOpenType(fromOe);

            IList fromDb;
            using (var dataContext = new OrderContext(OrderContextOptions.Create(true, null)))
            {
                fromDb = TestHelper.ExecuteDb(DataAdapter, dataContext, parameters.Expression, out IReadOnlyList<IncludeVisitor.Include> includesDb);
                includes.AddRange(includesDb);
            }

            TestHelper.Compare(fromDb, fromOe, DataAdapter.EntitySetAdapters.EdmModelMetadataProvider, includes);
        }
        private async Task<IList> ExecuteOe<T, TResult>(LambdaExpression lambda, int maxPageSize)
        {
            Container container = CreateContainer(maxPageSize);
            var visitor = new TypeMapperVisitor(container) { TypeMap = t => Type.GetType("ODataClient." + t.FullName) };
            var call = visitor.Visit(lambda.Body);

            IQueryable query = GetQuerableOe(container, typeof(T));
            Type elementType;
            if (call.Type.GetTypeInfo().IsGenericType)
            {
                elementType = call.Type.GetGenericArguments()[0];
                ExecuteQueryFuncAsync<Object> func = ExecuteQueryAsync<Object>;
                return await CreateDelegate(elementType, func)(query, call, visitor.NavigationPropertyAccessors);
            }
            else
            {
                elementType = typeof(Object);
                ExecuteQueryFunc<Object> func = ExecuteQueryScalar<Object>;
                return CreateDelegate(elementType, func)(query, call);
            }
        }
        protected virtual async Task<IList> ExecuteOeViaHttpClient<T, TResult>(QueryParameters<T, TResult> parameters)
        {
            Uri uri = CreateContainer(0).BaseUri;
            using (var client = new HttpClient())
            {
                client.BaseAddress = new UriBuilder(uri.Scheme, uri.Host, uri.Port).Uri;
                client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", OeRequestHeaders.JsonDefault.ContentType);
                using (HttpResponseMessage httpResponseMessage = await client.GetAsync(uri.LocalPath + "/" + parameters.RequestUri))
                    if (httpResponseMessage.IsSuccessStatusCode)
                    {
                        using (Stream content = await httpResponseMessage.Content.ReadAsStreamAsync())
                        {
                            var responseReader = new ResponseReader(EdmModel, DataAdapter);
                            return responseReader.Read<T>(content).Cast<Object>().ToList();
                        }
                    }
            }

            return null;
        }
        private async static Task<IList> ExecuteQueryAsync<T>(IQueryable query, Expression expression, IReadOnlyList<LambdaExpression> navigationPropertyAccessors)
        {
            var items = new List<T>();
            var newQuery = (DataServiceQuery<T>)query.Provider.CreateQuery<T>(expression);

            DataServiceQueryContinuation<T> continuation = null;
            for (var response = (QueryOperationResponse<T>)await newQuery.ExecuteAsync(); response != null;
                continuation = response.GetContinuation(), response = continuation == null ? null : (QueryOperationResponse<T>)await newQuery.Context.ExecuteAsync(continuation))
                foreach (T item in response)
                {
                    foreach (LambdaExpression navigationPropertyAccessor in navigationPropertyAccessors)
                    {
                        var propertyExpression = (MemberExpression)navigationPropertyAccessor.Body;
                        if (((PropertyInfo)propertyExpression.Member).GetValue(item) is IEnumerable property)
                        {
                            DataServiceQueryContinuation itemsContinuation = response.GetContinuation(property);
                            while (itemsContinuation != null)
                            {
                                QueryOperationResponse itemsResponse = await newQuery.Context.LoadPropertyAsync(item, propertyExpression.Member.Name, itemsContinuation);
                                itemsContinuation = itemsResponse.GetContinuation();
                            }
                        }
                    }
                    items.Add(item);
                }

            return items;
        }
        private static IList ExecuteQueryScalar<T>(IQueryable query, Expression expression)
        {
            T value = query.Provider.Execute<T>(expression);
            return new T[] { value };
        }
        private static List<IncludeVisitor.Include> GetIncludes(Db.OeDataAdapter dataAdapter, Expression expression)
        {
            var includes = new List<IncludeVisitor.Include>();
            var includeVisitor = new IncludeVisitor(dataAdapter.EntitySetAdapters.EdmModelMetadataProvider, dataAdapter.IsDatabaseNullHighestValue);
            includeVisitor.Visit(expression);
            foreach (IncludeVisitor.Include include in includeVisitor.Includes)
            {
                PropertyInfo property = include.Property;
                Type declaringType;
                if (property.DeclaringType == typeof(Category))
                    declaringType = typeof(ODataClient.OdataToEntity.Test.Model.Category);
                else if (property.DeclaringType == typeof(Customer))
                    declaringType = typeof(ODataClient.OdataToEntity.Test.Model.Customer);
                else if (property.DeclaringType == typeof(OrderItem))
                    declaringType = typeof(ODataClient.OdataToEntity.Test.Model.OrderItem);
                else if (property.DeclaringType == typeof(Order))
                    declaringType = typeof(ODataClient.OdataToEntity.Test.Model.Order);
                else if (property.DeclaringType == typeof(ShippingAddress))
                    declaringType = typeof(ODataClient.OdataToEntity.Test.Model.ShippingAddress);
                else if (property.DeclaringType == typeof(CustomerShippingAddress))
                    declaringType = typeof(ODataClient.OdataToEntity.Test.Model.CustomerShippingAddress);
                else
                    throw new InvalidOperationException("unknown type " + property.DeclaringType.FullName);

                PropertyInfo mapProperty = declaringType.GetProperty(property.Name);
                if (mapProperty == null)
                    throw new InvalidOperationException("unknown property " + property.ToString());

                includes.Add(new IncludeVisitor.Include(mapProperty, null));
            }
            return includes;
        }
        internal static IQueryable GetQuerableOe(Container container, Type entityType)
        {
            if (entityType == typeof(Category))
                return container.Categories;
            if (entityType == typeof(Customer))
                return container.Customers;
            if (entityType == typeof(OrderItem))
                return container.OrderItems;
            if (entityType == typeof(Order))
                return container.Orders;
            if (entityType == typeof(ManyColumns))
                return container.ManyColumns;
            if (entityType == typeof(ShippingAddress))
                return container.ShippingAddresses;
            if (entityType == typeof(CustomerShippingAddress))
                return container.CustomerShippingAddress;

            if (entityType == typeof(ODataClient.OdataToEntity.Test.Model.Category))
                return container.Categories;
            if (entityType == typeof(ODataClient.OdataToEntity.Test.Model.Customer))
                return container.Customers;
            if (entityType == typeof(ODataClient.OdataToEntity.Test.Model.OrderItem))
                return container.OrderItems;
            if (entityType == typeof(ODataClient.OdataToEntity.Test.Model.Order))
                return container.Orders;
            if (entityType == typeof(ODataClient.OdataToEntity.Test.Model.ManyColumns))
                return container.ManyColumns;
            if (entityType == typeof(ODataClient.OdataToEntity.Test.Model.ShippingAddress))
                return container.ShippingAddresses;
            if (entityType == typeof(ODataClient.OdataToEntity.Test.Model.CustomerShippingAddress))
                return container.CustomerShippingAddress;

            throw new InvalidOperationException("unknown type " + entityType.Name);
        }
        public async Task Initalize()
        {
            Container container = CreateContainer(0);
            await container.ResetDb().ExecuteAsync();
            await container.ResetManyColumns().ExecuteAsync();

            if (!_clear)
            {
                AspClient.BatchTest.Add(container);
                await container.SaveChangesAsync(SaveChangesOptions.BatchWithSingleChangeset);
            }
        }
        internal async static Task RunTest<T>(T testClass)
        {
            foreach (MethodInfo methodInfo in testClass.GetType().GetMethods())
            {
                Func<T, Task> testMethod;
                if (methodInfo.GetCustomAttribute(typeof(FactAttribute), false) != null)
                    testMethod = (Func<T, Task>)methodInfo.CreateDelegate(typeof(Func<T, Task>));
                else if (methodInfo.GetCustomAttribute(typeof(TheoryAttribute), false) != null)
                {
                    if (methodInfo.GetParameters().Length == 1 && methodInfo.GetParameters()[0].ParameterType == typeof(bool))
                    {
                        var methodCall = (Func<T, bool, Task>)methodInfo.CreateDelegate(typeof(Func<T, bool, Task>));
                        testMethod = async i => await methodCall(i, false);
                    }
                    else if (methodInfo.GetParameters().Length == 1 && methodInfo.GetParameters()[0].ParameterType == typeof(int))
                    {
                        var methodCall = (Func<T, int, Task>)methodInfo.CreateDelegate(typeof(Func<T, int, Task>));
                        testMethod = async i => { await methodCall(i, 0); await methodCall(i, 1); };
                    }
                    else
                    {
                        var methodCall = (Func<T, int, bool, Task>)methodInfo.CreateDelegate(typeof(Func<T, int, bool, Task>));
                        testMethod = async i => { await methodCall(i, 0, false); await methodCall(i, 1, false); };
                    }
                }
                else
                    continue;

                Console.WriteLine(methodInfo.Name);
                try
                {
                    await testMethod(testClass);
                }
                catch (NotSupportedException e)
                {
                    TestWriteException(e, ConsoleColor.Yellow);
                }
                catch (InvalidOperationException e)
                {
                    TestWriteException(e, ConsoleColor.Red);
                }
            }
        }
        private static void TestWriteException(Exception e, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(e.Message);
            Console.ResetColor();
        }

        public static Func<Container> ContainerFactory { get; set; }
        protected Db.OeDataAdapter DataAdapter { get; }
        protected IEdmModel EdmModel { get; }
    }

    public sealed class ManyColumnsFixtureInitDb : DbFixtureInitDb
    {
    }

}
