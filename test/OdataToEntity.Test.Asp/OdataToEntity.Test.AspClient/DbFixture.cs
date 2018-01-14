using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ODataClient.Default;
using OdataToEntity.Test.Model;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.ServiceModel;
using System.Threading.Tasks;
using Xunit;

namespace OdataToEntity.Test
{
    public partial class DbFixtureInitDb
    {
        private delegate IList ExecuteQueryFunc<out T>(IQueryable query, Expression expression);

        private readonly bool _clear;
        private String _databaseName;

        public DbFixtureInitDb(bool clear = false)
        {
            _clear = clear;
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
        public virtual Task Execute<T, TResult>(QueryParametersScalar<T, TResult> parameters)
        {
            IList fromOe = ExecuteOe<T, TResult>(parameters.Expression);
            IList fromDb;
            using (var dataContext = OrderContext.Create(_databaseName))
                fromDb = TestHelper.ExecuteDb<T, TResult>(dataContext, parameters.Expression);

            TestHelper.Compare(fromDb, fromOe, null);
            return Task.CompletedTask;
        }
        public virtual Task Execute<T, TResult>(QueryParameters<T, TResult> parameters)
        {
            IList fromOe = ExecuteOe<T, TResult>(parameters.Expression);
            List<IncludeVisitor.Include> includes = GetIncludes(parameters.Expression);
            if (typeof(TResult) == typeof(Object))
                fromOe = TestHelper.ToOpenType(fromOe);

            IList fromDb;
            using (var dataContext = OrderContext.Create(_databaseName))
            {
                fromDb = TestHelper.ExecuteDb<T, TResult>(dataContext, parameters.Expression, out IReadOnlyList<IncludeVisitor.Include> includesDb);
                includes.AddRange(includesDb);
            }

            TestHelper.Compare(fromDb, fromOe, includes);
            return Task.CompletedTask;
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

            return CreateDelegate(elementType, func)(query, call);
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
        private static List<IncludeVisitor.Include> GetIncludes(Expression expression)
        {
            var includes = new List<IncludeVisitor.Include>();
            var includeVisitor = new IncludeVisitor();
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
                else
                    throw new InvalidOperationException("unknown type " + property.DeclaringType.FullName);

                PropertyInfo mapProperty = declaringType.GetProperty(property.Name);
                if (mapProperty == null)
                    throw new InvalidOperationException("unknown property " + property.ToString());

                includes.Add(new IncludeVisitor.Include(mapProperty, null, false));
            }
            return includes;
        }
        private IQueryable GetQuerableOe<T>(Container container)
        {
            if (typeof(T) == typeof(Category))
                return container.Categories;
            if (typeof(T) == typeof(Customer))
                return container.Customers;
            if (typeof(T) == typeof(OrderItem))
                return container.OrderItems;
            if (typeof(T) == typeof(Order))
                return container.Orders;
            if (typeof(T) == typeof(ManyColumns))
                return container.ManyColumns;

            throw new InvalidOperationException("unknown type " + typeof(T).Name);
        }
        public virtual void Initalize()
        {
            _databaseName = OrderContext.GenerateDatabaseName();
            DbInit(_databaseName, _clear);
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
                        testMethod = i => methodCall(i, false);
                    }
                    if (methodInfo.GetParameters().Length == 1 && methodInfo.GetParameters()[0].ParameterType == typeof(int))
                    {
                        var methodCall = (Func<T, int, Task>)methodInfo.CreateDelegate(typeof(Func<T, int, Task>));
                        testMethod = i => methodCall(i, 0);
                    }
                    else
                    {
                        var methodCall = (Func<T, int, bool, Task>)methodInfo.CreateDelegate(typeof(Func<T, int, bool, Task>));
                        testMethod = i => methodCall(i, 0, false);
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
                catch (CommunicationObjectFaultedException e)
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
