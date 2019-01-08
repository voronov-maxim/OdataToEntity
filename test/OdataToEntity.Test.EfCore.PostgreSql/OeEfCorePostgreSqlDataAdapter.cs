using Microsoft.EntityFrameworkCore;
using System;
using OdataToEntity.Db;
using System.Collections;
using System.Collections.Generic;
using System.Data;

namespace OdataToEntity.EfCore
{
    public class StringList
    {
        public string item { get; set; }
    }
    public class OeEfCorePostgreSqlDataAdapter<T> : OeEfCoreDataAdapter<T> where T : DbContext
    {
        private sealed class OeEfCorePostgreSqlOperationAdapter : OeEfCoreOperationAdapter
        {
            public OeEfCorePostgreSqlOperationAdapter(Type dataContextType, OeEntitySetAdapterCollection entitySetAdapters)
                : base(dataContextType, entitySetAdapters)
            {
            }

            public override OeAsyncEnumerator ExecuteProcedureNonQuery(Object dataContext, String operationName, IReadOnlyList<KeyValuePair<String, Object>> parameters)
            {
                return base.ExecuteFunctionNonQuery(dataContext, operationName, parameters);
            }
            public override OeAsyncEnumerator ExecuteProcedureReader(Object dataContext, String operationName, IReadOnlyList<KeyValuePair<String, Object>> parameters, OeEntitySetAdapter entitySetAdapter)
            {
                return base.ExecuteFunctionReader(dataContext, operationName, parameters, entitySetAdapter);
            }
            public override OeAsyncEnumerator ExecuteProcedurePrimitive(Object dataContext, String operationName, IReadOnlyList<KeyValuePair<String, Object>> parameters, Type returnType)
            {
                return base.ExecuteFunctionPrimitive(dataContext, operationName, parameters, returnType);
            }
            protected override Object GetParameterCore(KeyValuePair<String, Object> parameter, String parameterName, int parameterIndex)
            {
                if (!(parameter.Value is String) && parameter.Value is IEnumerable list)
                {
                    var stringListList = new List<StringList>();
                    foreach (String item in list)
                        stringListList.Add(new StringList() { item = item });

                    if (parameterName == null)
                        parameterName = "p" + parameterIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    return new Npgsql.NpgsqlParameter(parameterName, stringListList);
                }

                return parameter.Value;
            }
        }

        public OeEfCorePostgreSqlDataAdapter() : this(null, null)
        {
        }
        public OeEfCorePostgreSqlDataAdapter(DbContextOptions options, Cache.OeQueryCache queryCache)
            : base(options, queryCache, new OeEfCorePostgreSqlOperationAdapter(typeof(T), _entitySetAdapters))
        {
            base.IsDatabaseNullHighestValue = true;
        }
    }
}
