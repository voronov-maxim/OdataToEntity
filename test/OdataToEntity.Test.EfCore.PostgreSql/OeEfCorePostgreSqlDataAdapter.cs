using Microsoft.EntityFrameworkCore;
using OdataToEntity.Db;
using System;
using System.Collections;
using System.Collections.Generic;

namespace OdataToEntity.EfCore
{
    public sealed class StringList
    {
        public String item { get; set; }
    }

    public class OeEfCorePostgreSqlDataAdapter<T> : OeEfCoreDataAdapter<T> where T : DbContext
    {
        private sealed class OeEfCorePostgreSqlOperationAdapter : OeEfCoreOperationAdapter
        {
            public OeEfCorePostgreSqlOperationAdapter(Type dataContextType)
                : base(dataContextType)
            {
            }

            public override IAsyncEnumerable<Object> ExecuteProcedureNonQuery(Object dataContext, String operationName, IReadOnlyList<KeyValuePair<String, Object>> parameters)
            {
                return base.ExecuteFunctionNonQuery(dataContext, operationName, parameters);
            }
            public override IAsyncEnumerable<Object> ExecuteProcedureReader(Object dataContext, String operationName, IReadOnlyList<KeyValuePair<String, Object>> parameters, OeEntitySetAdapter entitySetAdapter)
            {
                return base.ExecuteFunctionReader(dataContext, operationName, parameters, entitySetAdapter);
            }
            public override IAsyncEnumerable<Object> ExecuteProcedurePrimitive(Object dataContext, String operationName, IReadOnlyList<KeyValuePair<String, Object>> parameters, Type returnType)
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
            : base(options, queryCache, new OeEfCorePostgreSqlOperationAdapter(typeof(T)))
        {
            base.IsDatabaseNullHighestValue = true;
        }
    }
}
