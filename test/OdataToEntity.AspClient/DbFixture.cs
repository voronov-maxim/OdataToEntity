using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using ODataClient.Default;
using OdataToEntity.Test.Model;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace OdataToEntity.Test
{
    internal sealed partial class DbFixture
    {
        private delegate IList ExecuteQueryFunc<out T>(IQueryable query, Expression expression);

        private sealed class DbQueryVisitor<T> : ExpressionVisitor
        {
            private readonly IQueryable _query;
            private ConstantExpression _parameter;

            public DbQueryVisitor(IQueryable query)
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

        private sealed class TestContractResolver : DefaultContractResolver
        {
            protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
            {
                return base.CreateProperties(type, memberSerialization).OrderBy(p => p.PropertyName).ToList();
            }
        }

        private readonly String _databaseName;

        public DbFixture(bool clear = false)
        {
            _databaseName = OrderContext.GenerateDatabaseName();
            DbInit(_databaseName, clear);
        }

        public static Container CreateContainer()
        {
            return ContainerFactory();
        }
        private static ExecuteQueryFunc<Object> CreateDelegate(Type elementType, ExecuteQueryFunc<Object> execFunc)
        {
            MethodInfo execMethodInfo = execFunc.GetMethodInfo().GetGenericMethodDefinition().MakeGenericMethod(elementType);
            var execFuncType = execFunc.GetType().GetGenericTypeDefinition().MakeGenericType(elementType);
            return (ExecuteQueryFunc<Object>)execMethodInfo.CreateDelegate(execFuncType);
        }
        partial void DbInit(String databaseName, bool clear);
        public Task Execute<T, TResult>(QueryParametersScalar<T, TResult> parameters)
        {
            IList fromOe = ExecuteOe<T, TResult>(parameters.Expression);

            return Task.CompletedTask;
        }
        public Task Execute<T, TResult>(QueryParameters<T, TResult> parameters)
        {
            IList fromOe = ExecuteOe<T, TResult>(parameters.Expression);
            IList fromDb = ExecuteDb<T, TResult>(parameters.Expression);

            var settings = new JsonSerializerSettings()
            {
                ContractResolver = new TestContractResolver(),
                NullValueHandling = NullValueHandling.Ignore,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            };
            String jsonOe = JsonConvert.SerializeObject(fromOe, settings);
            String jsonDb = JsonConvert.SerializeObject(fromDb, settings);
            Xunit.Assert.Equal(jsonDb, jsonOe);

            return Task.CompletedTask;
        }
        private IList ExecuteDb<T, TResult>(LambdaExpression lambda)
        {
            using (var context = OrderContext.Create(_databaseName))
            {
                IQueryable query = GetQuerableDb<T>(context);
                var visitor = new DbQueryVisitor<T>(query);
                Expression call = visitor.Visit(lambda.Body);

                Type elementType;
                ExecuteQueryFunc<Object> func;
                if (call.Type.GetTypeInfo().IsGenericType)
                {
                    elementType = call.Type.GetGenericArguments()[0];
                    func = ExecuteQuery<Object>;
                }
                else
                {
                    elementType = typeof(Object);
                    func = ExecuteQueryScalar<Object>;
                }

                IList fromDb = CreateDelegate(elementType, func)(query, call);

                IReadOnlyList<PropertyInfo> includeProperties = TestHelper.GetIncludeProperties(lambda);
                TestHelper.SetNullCollection(fromDb, includeProperties);
                return fromDb;
            }
        }
        private IList ExecuteOe<T, TResult>(LambdaExpression lambda)
        {
            Container container = CreateContainer();
            IQueryable query = GetQuerableOe<T>(container);
            var visitor = new TypeMapperVisitor(query) { TypeMap = t => Type.GetType("ODataClient." + t.FullName) };
            var call = visitor.Visit(lambda.Body);

            Type elementType;
            ExecuteQueryFunc<Object> func;
            if (call.Type.GetTypeInfo().IsGenericType)
            {
                elementType = call.Type.GetGenericArguments()[0];
                func = ExecuteQuery<Object>;
            }
            else
            {
                elementType = typeof(Object);
                func = ExecuteQueryScalar<Object>;
            }

            IList fromOe = CreateDelegate(elementType, func)(query, call);

            TestHelper.SetNullCollection(fromOe, GetIncludeProperties(lambda));
            return fromOe;
        }
        private static IList ExecuteQuery<T>(IQueryable query, Expression expression)
        {
            IQueryable<T> newQuery = query.Provider.CreateQuery<T>(expression);
            return newQuery.ToList();
        }
        private static IList ExecuteQueryScalar<T>(IQueryable query, Expression expression)
        {
            T value = query.Provider.Execute<T>(expression);
            return new T[] { value };
        }
        private static IReadOnlyList<PropertyInfo> GetIncludeProperties(Expression expression)
        {
            var includeProperties = new List<PropertyInfo>();
            foreach (PropertyInfo property in TestHelper.GetIncludeProperties(expression))
            {
                Type declaringType;
                if (property.DeclaringType == typeof(Customer))
                    declaringType = typeof(ODataClient.OdataToEntity.Test.Model.Customer);
                else if (property.DeclaringType == typeof(OrderItem))
                    declaringType = typeof(ODataClient.OdataToEntity.Test.Model.OrderItem);
                else if (property.DeclaringType == typeof(Order))
                    declaringType = typeof(ODataClient.OdataToEntity.Test.Model.Order);
                else
                    throw new InvalidOperationException("unknown type " + property.DeclaringType.FullName);

                PropertyInfo mapProperty = declaringType.GetProperty(property.Name);
                if (mapProperty == null)
                    throw new InvalidOperationException("unknown property " + property.ToString());

                includeProperties.Add(mapProperty);
            }
            return includeProperties;
        }
        private IQueryable GetQuerableDb<T>(OrderContext context)
        {
            if (typeof(T) == typeof(Customer))
                return context.Customers;
            if (typeof(T) == typeof(OrderItem))
                return context.OrderItems;
            if (typeof(T) == typeof(Order))
                return context.Orders;

            throw new InvalidOperationException("unknown type " + typeof(T).Name);
        }
        private IQueryable GetQuerableOe<T>(Container container)
        {
            if (typeof(T) == typeof(Customer))
                return container.Customers;
            if (typeof(T) == typeof(OrderItem))
                return container.OrderItems;
            if (typeof(T) == typeof(Order))
                return container.Orders;

            throw new InvalidOperationException("unknown type " + typeof(T).Name);
        }
        internal async static Task RunTest<T>(T testClass)
        {
            foreach (MethodInfo methodInfo in testClass.GetType().GetMethods().Where(m => m.GetCustomAttributes(typeof(FactAttribute), false).Length == 1))
            {
                var testMethod = (Func<T, Task>)methodInfo.CreateDelegate(typeof(Func<T, Task>));
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

        public static Func<Container> ContainerFactory
        {
            get;
            set;
        }
    }
}
