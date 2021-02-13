using Microsoft.EntityFrameworkCore;
using Microsoft.OData.UriParser;
using OdataToEntity.Cache;
using OdataToEntity.Cache.UriCompare;
using OdataToEntity.Parsers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace OdataToEntity.Test
{
    internal sealed class SelectTestDefinition
    {
        private sealed class ExpressionClosure<T, TResult>
        {
            private readonly Expression<Func<IQueryable<T>, IQueryable<TResult>>> _expression;

            public ExpressionClosure(Expression<Func<IQueryable<T>, IQueryable<TResult>>> expression)
            {
                _expression = expression;
            }

            public IList Execute(Db.OeDataAdapter dataAdapter, DbContext dbContext)
            {
                return TestHelper.ExecuteDb(dataAdapter, dbContext, _expression, out _);
            }
        }

        private sealed class ExpressionScalarClosure<T, TResult>
        {
            private readonly Expression<Func<IQueryable<T>, TResult>> _expression;

            public ExpressionScalarClosure(Expression<Func<IQueryable<T>, TResult>> expression)
            {
                _expression = expression;
            }

            public IList Execute(Db.OeDataAdapter dataAdapter, DbContext dbContext)
            {
                return TestHelper.ExecuteDb(dataAdapter.EntitySetAdapters, dbContext, _expression);
            }
        }

#if !IGNORE_PLNull
        private sealed class SelectTestDefinitionFixture : PLNull_DbFixtureInitDb
#elif IGNORE_PLNull && !IGNORE_RDBNull
        private sealed class SelectTestDefinitionFixture : RDBNull_DbFixtureInitDb
#endif
        {
            private readonly List<SelectTestDefinition> _selectTestDefinitions;

            public SelectTestDefinitionFixture()
            {
                _selectTestDefinitions = new List<SelectTestDefinition>();
            }

            public override Task Execute<T, TResult>(QueryParameters<T, TResult> parameters)
            {
                var executorDb = (Func<Db.OeDataAdapter, DbContext, IList>)new ExpressionClosure<T, TResult>(parameters.Expression).Execute;
                _selectTestDefinitions.Add(new SelectTestDefinition(parameters.RequestUri, executorDb));
                return Task.CompletedTask;
            }
            public override Task Execute<T, TResult>(QueryParametersScalar<T, TResult> parameters)
            {
                var executorDb = (Func<Db.OeDataAdapter, DbContext, IList>)new ExpressionScalarClosure<T, TResult>(parameters.Expression).Execute;
                _selectTestDefinitions.Add(new SelectTestDefinition(parameters.RequestUri, executorDb));
                return Task.CompletedTask;

            }
            public override Task Initalize()
            {
                return Task.CompletedTask;
            }

            public IReadOnlyList<SelectTestDefinition> SelectTestDefinitions => _selectTestDefinitions;
        }

        public SelectTestDefinition(String request, Func<Db.OeDataAdapter, DbContext, IList> executorDb)
        {
            Request = request;
            ExecutorDb = executorDb;
        }

        public static SelectTestDefinition[] GetSelectTestDefinitions()
        {
            var fixture = new SelectTestDefinitionFixture();
#if !IGNORE_PLNull
            var selectTest = new PLNull(fixture);
#elif IGNORE_PLNull && !IGNORE_RDBNull
            var selectTest = new RDBNull(fixture);
#endif

            var methodNames = new List<String>();
            foreach (MethodInfo methodInfo in selectTest.GetType().GetMethods().Where(m => m.GetCustomAttributes(typeof(FactAttribute), false).Count() == 1))
            {
                if (methodInfo.IsDefined(typeof(NotPerformanceCahe)))
                    continue;

                Func<SelectTest, Task> testMethod;
                var factAttribute = (FactAttribute)methodInfo.GetCustomAttribute(typeof(FactAttribute), false);
                if (factAttribute is TheoryAttribute)
                {
                    if (methodInfo.GetParameters().Length == 1)
                    {
                        if (methodInfo.GetParameters()[0].ParameterType == typeof(bool))
                        {
                            var methodCall = (Func<SelectTest, bool, Task>)methodInfo.CreateDelegate(typeof(Func<SelectTest, bool, Task>));
                            testMethod = i => methodCall(i, false);
                        }
                        else
                        {
                            var methodCall = (Func<SelectTest, int, Task>)methodInfo.CreateDelegate(typeof(Func<SelectTest, int, Task>));
                            testMethod = i => methodCall(i, 0);
                        }
                    }
                    else
                    {
                        var methodCall = (Func<SelectTest, int, bool, Task>)methodInfo.CreateDelegate(typeof(Func<SelectTest, int, bool, Task>));
                        testMethod = i => methodCall(i, 0, false);
                    }
                }
                else
                    testMethod = (Func<SelectTest, Task>)methodInfo.CreateDelegate(typeof(Func<SelectTest, Task>));

                int count = fixture.SelectTestDefinitions.Count;
                testMethod(selectTest).GetAwaiter().GetResult();
                if (fixture.SelectTestDefinitions.Count == count)
                    continue;

                methodNames.Add(methodInfo.Name);
            }

            for (int i = 0; i < methodNames.Count; i++)
                fixture.SelectTestDefinitions[i].MethodName = methodNames[i];
            return fixture.SelectTestDefinitions.ToArray();
        }

        public Func<Db.OeDataAdapter, DbContext, IList> ExecutorDb { get; }
        public String MethodName { get; set; }
        public String Request { get; }

        public override String ToString() => Request;
    }

    public sealed class QueryComparerTest
    {
        private sealed class FakeReadOnlyDictionary<TKey, TValue> : Dictionary<TKey, TValue>, IReadOnlyDictionary<TKey, TValue>
        {
            TValue IReadOnlyDictionary<TKey, TValue>.this[TKey key] {
                get
                {
                    if (base.TryGetValue(key, out TValue value))
                        return value;

                    var constantNode = (ConstantNode)(Object)key;
                    Type type = constantNode.Value == null ? typeof(Object) : constantNode.Value.GetType();
                    value = (TValue)(Object)new OeQueryCacheDbParameterDefinition("p_" + base.Count.ToString(CultureInfo.InvariantCulture), type);
                    base[key] = value;
                    return value;
                }
            }
        }

        //[Fact]
        public void CacheCode()
        {
            var hashes = new Dictionary<int, List<String>>();

            SelectTestDefinition[] requestMethodNames = SelectTestDefinition.GetSelectTestDefinitions();

            var fixture = new RDBNull_DbFixtureInitDb();
            for (int i = 0; i < requestMethodNames.Length; i++)
            {
                var queryContext = new OeQueryContext(fixture.OeEdmModel, fixture.ParseUri(requestMethodNames[i].Request));
                int hash = OeCacheComparer.GetCacheCode(queryContext.CreateCacheContext());
                if (!hashes.TryGetValue(hash, out List<String> value))
                {
                    value = new List<String>();
                    hashes.Add(hash, value);
                }
                value.Add(requestMethodNames[i].MethodName);
            }

            var duplicate = hashes.Where(p => p.Value.Count > 1).Select(p => p.Value).ToArray();
        }
        public void Test()
        {
            SelectTestDefinition[] requestMethodNames = SelectTestDefinition.GetSelectTestDefinitions();
            requestMethodNames = requestMethodNames.Where(t => t.MethodName == "FilterEnum" || t.MethodName == "FilterEnumNull").ToArray();

            var fixture = new RDBNull_DbFixtureInitDb();
            for (int i = 0; i < requestMethodNames.Length; i++)
            {
                var queryContext1 = new OeQueryContext(fixture.OeEdmModel, fixture.ParseUri(requestMethodNames[i].Request));
                var queryContext2 = new OeQueryContext(fixture.OeEdmModel, fixture.ParseUri(requestMethodNames[i].Request));

                var constantToParameterMapper = new FakeReadOnlyDictionary<ConstantNode, OeQueryCacheDbParameterDefinition>();
                if (queryContext1.ODataUri.Skip != null)
                {
                    var constantNode = OeCacheComparerParameterValues.CreateSkipConstantNode((int)queryContext1.ODataUri.Skip.Value, queryContext1.ODataUri.Path);
                    constantToParameterMapper.Add(constantNode, new OeQueryCacheDbParameterDefinition("p_0", typeof(int)));
                }
                if (queryContext1.ODataUri.Top != null)
                {
                    var constantNode = OeCacheComparerParameterValues.CreateTopConstantNode((int)queryContext1.ODataUri.Top.Value, queryContext1.ODataUri.Path);
                    constantToParameterMapper.Add(constantNode, new OeQueryCacheDbParameterDefinition($"p_{constantToParameterMapper.Count}", typeof(int)));
                }

                OeCacheContext cacheContext1 = queryContext1.CreateCacheContext();
                OeCacheContext cacheContext2 = queryContext2.CreateCacheContext();
                bool result = new OeCacheComparer(constantToParameterMapper).Compare(cacheContext1, cacheContext2);
                Assert.True(result);

                for (int j = i + 1; j < requestMethodNames.Length; j++)
                {
                    queryContext2 = new OeQueryContext(fixture.OeEdmModel, fixture.ParseUri(requestMethodNames[j].Request));

                    constantToParameterMapper = new FakeReadOnlyDictionary<ConstantNode, OeQueryCacheDbParameterDefinition>();
                    result = new OeCacheComparer(constantToParameterMapper).Compare(cacheContext1, cacheContext2);
                    Assert.False(result);
                }
            }
        }
    }
}
