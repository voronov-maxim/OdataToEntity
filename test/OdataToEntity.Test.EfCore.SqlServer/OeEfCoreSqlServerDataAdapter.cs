using Microsoft.EntityFrameworkCore;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;

namespace OdataToEntity.EfCore
{
    public class OeEfCoreSqlServerDataAdapter<T> : OeEfCoreDataAdapter<T> where T : DbContext
    {
        private sealed class OeEfCoreSqlServerOperationAdapter : OeEfCoreOperationAdapter
        {
            public OeEfCoreSqlServerOperationAdapter(Type dataContextType)
                : base(dataContextType)
            {
            }

            protected override Object GetParameterCore(KeyValuePair<String, Object> parameter, String parameterName, int parameterIndex)
            {
                if (!(parameter.Value is String) && parameter.Value is IEnumerable list)
                {
                    DataTable table = Infrastructure.OeDataTableHelper.GetDataTable(list);
                    if (parameterName == null)
                        parameterName = "@p" + parameterIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    return new Microsoft.Data.SqlClient.SqlParameter(parameterName, table) { TypeName = parameter.Key };
                }

                return parameter.Value;
            }
        }

        public OeEfCoreSqlServerDataAdapter() : this(null, null)
        {
        }
        public OeEfCoreSqlServerDataAdapter(Cache.OeQueryCache queryCache) : this(null, queryCache)
        {
        }
        public OeEfCoreSqlServerDataAdapter(DbContextOptions options, Cache.OeQueryCache queryCache)
            : base(options, queryCache, new OeEfCoreSqlServerOperationAdapter(typeof(T)))
        {
        }
    }
}
