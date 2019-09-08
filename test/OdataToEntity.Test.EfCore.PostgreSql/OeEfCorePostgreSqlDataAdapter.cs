using Microsoft.EntityFrameworkCore;
using System;
using System.Collections;
using System.Collections.Generic;

namespace OdataToEntity.EfCore.Postgresql
{
    public sealed class StringList
    {
        public String item { get; set; }
    }

    public class EfCorePostgreSqlDataAdapter<T> : OeEfCorePostgreSqlDataAdapter<T> where T : DbContext
    {
        private sealed class EfCorePostgreSqlOperationAdapter : OePostgreSqlEfCoreOperationAdapter
        {
            public EfCorePostgreSqlOperationAdapter(Type dataContextType) : base(dataContextType)
            {
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

        public EfCorePostgreSqlDataAdapter() : this(null, null)
        {
        }
        public EfCorePostgreSqlDataAdapter(DbContextOptions options, Cache.OeQueryCache queryCache)
            : base(options, queryCache, new EfCorePostgreSqlOperationAdapter(typeof(T)))
        {
        }
    }
}
