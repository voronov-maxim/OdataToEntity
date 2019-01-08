using LinqToDB.Data;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace OdataToEntity.Linq2Db
{
    public class OeLinq2DbSqlServerDataAdapter<T> : OeLinq2DbDataAdapter<T> where T : DataConnection, IOeLinq2DbDataContext
    {
        private sealed class OeLinq2DbSqlServerOperationAdapter : OeLinq2DbOperationAdapter
        {
            public OeLinq2DbSqlServerOperationAdapter(Type dataContextType)
                : base(dataContextType)
            {
            }

            protected override Object GetParameterCore(KeyValuePair<String, Object> parameter, String parameterName, int parameterIndex)
            {
                if (!(parameter.Value is String) && parameter.Value is IEnumerable list)
                {
                    DataTable table = Infrastructure.OeDataTableHelper.GetDataTable(list);
                    return new DataParameter(parameter.Key, table) { DbType = parameter.Key };
                }

                return parameter.Value;
            }
        }

        public OeLinq2DbSqlServerDataAdapter() : this(null)
        {
        }
        public OeLinq2DbSqlServerDataAdapter(Cache.OeQueryCache queryCache)
            : base(queryCache, new OeLinq2DbSqlServerOperationAdapter(typeof(T)))
        {
        }
    }
}
