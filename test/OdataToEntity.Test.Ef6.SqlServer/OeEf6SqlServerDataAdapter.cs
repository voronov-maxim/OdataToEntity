using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Data.SqlClient;

namespace OdataToEntity.Ef6
{
    public class OeEf6SqlServerDataAdapter<T> : OeEf6DataAdapter<T> where T : DbContext
    {
        private sealed class OeEf6SqlServerOperationAdapter : OeEf6OperationAdapter
        {
            public OeEf6SqlServerOperationAdapter(Type dataContextType)
                : base(dataContextType)
            {
            }

            protected override Object GetParameterCore(KeyValuePair<String, Object> parameter, String parameterName,  int parameterIndex)
            {
                if (!(parameter.Value is String) && parameter.Value is IEnumerable list)
                {
                    DataTable table = Infrastructure.OeDataTableHelper.GetDataTable(list);
                    if (parameterName == null)
                        parameterName = "@p" + parameterIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    return new SqlParameter(parameterName, table) { TypeName = parameter.Key };
                }

                return parameter.Value;
            }
        }

        public OeEf6SqlServerDataAdapter() : this(null)
        {
        }
        public OeEf6SqlServerDataAdapter(Cache.OeQueryCache queryCache)
            : base(queryCache, new OeEf6SqlServerOperationAdapter(typeof(T)))
        {
        }
    }
}
