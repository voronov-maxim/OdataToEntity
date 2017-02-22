using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using OdataToEntity.Parsers;
using OdataToEntity.Parsers.UriCompare;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace OdataToEntity.Test
{
    public sealed class QueryComparerTest
    {
        private sealed class FakeReadOnlyDictionary<TKey, TValue> : Dictionary<TKey, TValue>, IReadOnlyDictionary<TKey, TValue>
        {
            TValue IReadOnlyDictionary<TKey, TValue>.this[TKey key]
            {
                get
                {
                    TValue value;
                    if (base.TryGetValue(key, out value))
                        return value;

                    var constantNode = (ConstantNode)(Object)key;
                    Type type = constantNode.Value == null ? typeof(Object) : constantNode.Value.GetType();
                    value = (TValue)(Object)new Db.OeQueryCacheDbParameterDefinition("p_" + base.Count.ToString(), type);
                    base[key] = value;
                    return value;
                }
            }
        }

        private struct RequestMethodName
        {
            public readonly String MethodName;
            public readonly String Request;

            public RequestMethodName(String request, String methodName)
            {
                Request = request;
                MethodName = methodName;
            }
        }

        private sealed class QueryComparerFixture : DbFixtureInitDb
        {
            private readonly List<RequestMethodName> _requests;

            public QueryComparerFixture()
            {
                _requests = new List<RequestMethodName>();
            }

            public override Task Execute<T, TResult>(QueryParameters<T, TResult> parameters)
            {
                _requests.Add(new RequestMethodName(parameters.RequestUri, null));
                return Task.CompletedTask;
            }
            public override Task Execute<T, TResult>(QueryParametersScalar<T, TResult> parameters)
            {
                _requests.Add(new RequestMethodName(parameters.RequestUri, null));
                return Task.CompletedTask;

            }
            public override void Initalize()
            {
            }

            public IReadOnlyList<RequestMethodName> Requests => _requests;
        }

        private static async Task<RequestMethodName[]> GetRequestMethodNames(QueryComparerFixture fixture)
        {
            var methodNames = new List<String>();
            var selectTest = new SelectTest(fixture);
            foreach (MethodInfo methodInfo in selectTest.GetType().GetMethods().Where(m => m.GetCustomAttributes(typeof(FactAttribute), false).Count() == 1))
            {
                methodNames.Add(methodInfo.Name);
                var testMethod = (Func<SelectTest, Task>)methodInfo.CreateDelegate(typeof(Func<SelectTest, Task>));
                await testMethod(selectTest);
            }

            var requestMethodNames = new RequestMethodName[methodNames.Count];
            for (int i = 0; i < methodNames.Count; i++)
                requestMethodNames[i] = new RequestMethodName(fixture.Requests[i].Request, methodNames[i]);
            return requestMethodNames;
        }
        //[Fact]
        public async Task CacheCode()
        {
            var hashes = new Dictionary<int, List<String>>();

            var fixture = new QueryComparerFixture();
            RequestMethodName[] requestMethodNames = await GetRequestMethodNames(fixture);

            var parser = new OeGetParser(new Uri("http://dummy/"), fixture.OeDataAdapter, fixture.EdmModel);
            for (int i = 0; i < requestMethodNames.Length; i++)
            {
                OeParseUriContext parseUriContext = parser.ParseUri(new Uri(parser.BaseUri + requestMethodNames[i].Request));
                int hash = OeODataUriComparer.GetCacheCode(parseUriContext);
                List<String> value;
                if (!hashes.TryGetValue(hash, out value))
                {
                    value = new List<String>();
                    hashes.Add(hash, value);
                }
                value.Add(requestMethodNames[i].MethodName);
            }

            var duplicate = hashes.Where(p => p.Value.Count > 1).Select(p => p.Value).ToArray();
        }
        public async Task Test()
        {
            var fixture = new QueryComparerFixture();
            RequestMethodName[] requestMethodNames = await GetRequestMethodNames(fixture);
            requestMethodNames = requestMethodNames.Where(t => t.MethodName == "FilterEnum" || t.MethodName == "FilterEnumNull").ToArray();

            var parser = new OeGetParser(new Uri("http://dummy/"), fixture.OeDataAdapter, fixture.EdmModel);
            for (int i = 0; i < requestMethodNames.Length; i++)
            {
                OeParseUriContext parseUriContext1 = parser.ParseUri(new Uri(parser.BaseUri + requestMethodNames[i].Request));
                OeParseUriContext parseUriContext2 = parser.ParseUri(new Uri(parser.BaseUri + requestMethodNames[i].Request));

                var constantToParameterMapper = new FakeReadOnlyDictionary<ConstantNode, Db.OeQueryCacheDbParameterDefinition>();
                if (parseUriContext1.ODataUri.Skip != null)
                {
                    var constantNode = OeODataUriComparerParameterValues.CreateSkipConstantNode((int)parseUriContext1.ODataUri.Skip.Value, parseUriContext1.ODataUri.Path);
                    constantToParameterMapper.Add(constantNode, new Db.OeQueryCacheDbParameterDefinition("p_0", typeof(int)));
                }
                if (parseUriContext1.ODataUri.Top != null)
                {
                    var constantNode = OeODataUriComparerParameterValues.CreateTopConstantNode((int)parseUriContext1.ODataUri.Top.Value, parseUriContext1.ODataUri.Path);
                    constantToParameterMapper.Add(constantNode, new Db.OeQueryCacheDbParameterDefinition($"p_{constantToParameterMapper.Count}", typeof(int)));
                }

                bool result = new OeODataUriComparer(constantToParameterMapper).Compare(parseUriContext1, parseUriContext2);
                Assert.True(result);

                for (int j = i + 1; j < requestMethodNames.Length; j++)
                {
                    parseUriContext1 = parser.ParseUri(new Uri(parser.BaseUri + requestMethodNames[i].Request));
                    parseUriContext2 = parser.ParseUri(new Uri(parser.BaseUri + requestMethodNames[j].Request));
                    constantToParameterMapper = new FakeReadOnlyDictionary<ConstantNode, Db.OeQueryCacheDbParameterDefinition>();
                    result = new OeODataUriComparer(constantToParameterMapper).Compare(parseUriContext1, parseUriContext2);
                    Assert.False(result);
                }
            }
        }
    }
}
