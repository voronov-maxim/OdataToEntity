using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
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
    internal sealed class SelectTestDefinition
    {
        private readonly Func<DbContext, IList> _executorDb;
        private readonly String _request;

        public SelectTestDefinition(String request, Func<DbContext, IList> executorDb)
        {
            _request = request;
            _executorDb = executorDb;
        }

        public Func<DbContext, IList> ExecutorDb => _executorDb;
        public String MethodName { get; set; }
        public String Request => _request;

        public override String ToString() => _request;
    }

    internal partial class TestHelper
    {
        private sealed class ExpressionClosure<T, TResult>
        {
            private readonly Expression<Func<IQueryable<T>, IQueryable<TResult>>> _expression;

            public ExpressionClosure(Expression<Func<IQueryable<T>, IQueryable<TResult>>> expression)
            {
                _expression = expression;
            }

            public IList Execute(DbContext dbContext)
            {
                return TestHelper.ExecuteDb(dbContext, _expression);
            }
        }

        private sealed class ExpressionScalarClosure<T, TResult>
        {
            private readonly Expression<Func<IQueryable<T>, TResult>> _expression;

            public ExpressionScalarClosure(Expression<Func<IQueryable<T>, TResult>> expression)
            {
                _expression = expression;
            }

            public IList Execute(DbContext dbContext)
            {
                return TestHelper.ExecuteDb(dbContext, _expression);
            }
        }

        private sealed class SelectTestDefinitionFixture : DbFixtureInitDb
        {
            private readonly List<SelectTestDefinition> _selectTestDefinitions;

            public SelectTestDefinitionFixture()
            {
                _selectTestDefinitions = new List<SelectTestDefinition>();
            }

            public override Task Execute<T, TResult>(QueryParameters<T, TResult> parameters)
            {
                var executorDb = (Func<DbContext, IList>)new ExpressionClosure<T, TResult>(parameters.Expression).Execute;
                _selectTestDefinitions.Add(new SelectTestDefinition(parameters.RequestUri, executorDb));
                return Task.CompletedTask;
            }
            public override Task Execute<T, TResult>(QueryParametersScalar<T, TResult> parameters)
            {
                var executorDb = (Func<DbContext, IList>)new ExpressionScalarClosure<T, TResult>(parameters.Expression).Execute;
                _selectTestDefinitions.Add(new SelectTestDefinition(parameters.RequestUri, executorDb));
                return Task.CompletedTask;

            }
            public override void Initalize()
            {
            }

            public IReadOnlyList<SelectTestDefinition> SelectTestDefinitions => _selectTestDefinitions;
        }

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

        public sealed class TestContractResolver : DefaultContractResolver
        {
            protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
            {
                return base.CreateProperties(type, memberSerialization).OrderBy(p => p.PropertyName).ToList();
            }
        }

        public static IList ExecuteDb<T, TResult>(DbContext dataContext, Expression<Func<IQueryable<T>, TResult>> expression)
        {
            IQueryable<T> query = GetQuerableDb<T>(dataContext);
            var visitor = new QueryVisitor<T>(query);
            Expression call = visitor.Visit(expression.Body);
            return new[] { query.Provider.Execute<TResult>(call).ToString() };
        }
        public static IList ExecuteDb<T, TResult>(DbContext dataContext, Expression<Func<IQueryable<T>, IQueryable<TResult>>> expression)
        {
            IQueryable<T> query = GetQuerableDb<T>(dataContext);
            var visitor = new QueryVisitor<T>(query);
            Expression call = visitor.Visit(expression.Body);

            var includeVisitor = new IncludeVisitor();
            call = includeVisitor.Visit(call);

            IList fromDb = query.Provider.CreateQuery<TResult>(call).ToList();
            if (typeof(TResult) == typeof(Object))
                fromDb = SortProperty(fromDb);
            else
                SetNullCollection(fromDb, includeVisitor.Includes);
            return fromDb;
        }
        private static IQueryable<T> GetQuerableDb<T>(DbContext dataContext)
        {
            var orderContext = (OrderContext)dataContext;
            if (typeof(T) == typeof(Customer))
                return (IQueryable<T>)orderContext.Customers;
            if (typeof(T) == typeof(OrderItem))
                return (IQueryable<T>)orderContext.OrderItems;
            if (typeof(T) == typeof(Order))
                return (IQueryable<T>)orderContext.Orders;

            throw new InvalidOperationException("unknown type " + typeof(T).Name);
        }
        public static SelectTestDefinition[] GetSelectTestDefinitions()
        {
            var fixture = new SelectTestDefinitionFixture();
            var selectTest = new SelectTest(fixture);

            var methodNames = new List<String>();
            foreach (MethodInfo methodInfo in selectTest.GetType().GetTypeInfo().GetMethods().Where(m => m.GetCustomAttributes(typeof(FactAttribute), false).Count() == 1))
            {
                methodNames.Add(methodInfo.Name);
                var testMethod = (Func<SelectTest, Task>)methodInfo.CreateDelegate(typeof(Func<SelectTest, Task>));
                testMethod(selectTest).GetAwaiter().GetResult();
            }

            for (int i = 0; i < methodNames.Count; i++)
                fixture.SelectTestDefinitions[i].MethodName = methodNames[i];
            return fixture.SelectTestDefinitions.ToArray();
        }
        private static bool IsCollection(Type collectionType)
        {
            if (collectionType.GetTypeInfo().IsGenericType && collectionType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                return true;

            foreach (Type iface in collectionType.GetTypeInfo().GetInterfaces())
                if (iface.GetTypeInfo().IsGenericType && iface.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                    return true;

            return false;
        }
        public static bool IsEntity(Type type)
        {
            TypeInfo typeInfo = type.GetTypeInfo();
            if (typeInfo.IsPrimitive)
                return false;
            if (typeInfo.IsValueType)
                return false;
            if (type == typeof(String))
                return false;
            return true;
        }
        private static IList Lambda(IEnumerable source, Delegate lambda)
        {
            return LambdaT((dynamic)source, (dynamic)lambda);
        }
        private static IList LambdaT<T>(IEnumerable<T> source, Delegate lambda)
        {
            var result = (IEnumerable)lambda.DynamicInvoke(source);
            return result.Cast<T>().ToArray();
        }
        public static void SetNullCollection(IList rootItems, IEnumerable<IncludeVisitor.Include> includes)
        {
            var visited = new HashSet<Object>();
            var includesDistinct = new Dictionary<PropertyInfo, Delegate>();
            foreach (IncludeVisitor.Include include in includes)
                includesDistinct[include.Property] = include.Lambda;

            foreach (Object root in rootItems)
                SetNullCollection(root, visited, includesDistinct);
        }
        private static void SetNullCollection(Object entity, HashSet<Object> visited, Dictionary<PropertyInfo, Delegate> includes)
        {
            if (entity == null || visited.Contains(entity))
                return;

            visited.Add(entity);
            foreach (PropertyInfo property in entity.GetType().GetTypeInfo().GetProperties())
                if (IsEntity(property.PropertyType))
                {
                    Object value = property.GetValue(entity);
                    if (value == null)
                        continue;

                    if (!includes.ContainsKey(property))
                        if (property.CanWrite)
                        {
                            property.SetValue(entity, null);
                            continue;
                        }

                    if (IsCollection(property.PropertyType))
                    {
                        bool isEmpty = true;
                        foreach (Object item in (IEnumerable)value)
                        {
                            isEmpty = false;
                            SetNullCollection(item, visited, includes);
                        }
                        if (isEmpty)
                            property.SetValue(entity, null);
                        else
                        {
                            Delegate lambda;
                            if (includes.TryGetValue(property, out lambda) && lambda != null)
                            {
                                IList list = Lambda((IEnumerable)value, lambda);
                                if (list.Count == 0)
                                    property.SetValue(entity, null);
                                else
                                    property.SetValue(entity, list);
                            }
                        }
                    }
                    else
                        SetNullCollection(value, visited, includes);
                }
        }
        public static JObject[] SortProperty(IEnumerable<JObject> items)
        {
            var jobjects = new List<JObject>();
            foreach (Object item in items)
            {
                var jobject = new JObject();
                foreach (JProperty jpropety in (item as JObject).Properties().OrderBy(p => p.Name))
                    if (jpropety.Value is JObject)
                        jobject.Add(jpropety.Name, SortProperty(jpropety.Value as JObject));
                    else
                    {
                        var jvalue = jpropety.Value as JValue;
                        if (jvalue != null && jvalue.Value is Decimal) //fix precision sql.avg decimal(38,6)
                        {
                            var value = (Decimal)jvalue.Value;
                            jvalue.Value = Math.Round(value, 2);
                        }
                        jobject.Add(jpropety.Name, jpropety.Value);
                    }
                jobjects.Add(jobject);
            }
            return jobjects.ToArray();
        }
        public static IList SortProperty(IEnumerable items)
        {
            var serializer = new JsonSerializer();
            serializer.ContractResolver = new TestContractResolver();
            return JArray.FromObject(items, serializer);
        }
        private static JObject SortProperty(JObject jobject)
        {
            var innerObject = new JObject();
            foreach (var pair in ((IEnumerable<KeyValuePair<String, JToken>>)jobject).OrderBy(p => p.Key))
                if (pair.Value is JObject)
                    innerObject.Add(pair.Key, SortProperty(pair.Value as JObject));
                else
                    innerObject.Add(pair.Key, pair.Value);
            return innerObject;
        }
    }
}
