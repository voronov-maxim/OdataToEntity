using Microsoft.EntityFrameworkCore;
using System;
using OdataToEntity.Db;
using System.Collections.Generic;

namespace OdataToEntity.EfCore
{
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
            public override OeAsyncEnumerator ExecuteProcedureScalar(Object dataContext, String operationName, IReadOnlyList<KeyValuePair<String, Object>> parameters, Type returnType)
            {
                return base.ExecuteFunctionScalar(dataContext, operationName, parameters, returnType);
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
