using System;
using System.Collections.Generic;
using System.Text;

namespace OdataToEntity.EfCore
{
    public sealed class OeMySqlEfCoreOperationAdapter : OeEfCoreOperationAdapter
    {
        public OeMySqlEfCoreOperationAdapter(Type dataContextType) : base(dataContextType)
        {
        }
        public OeMySqlEfCoreOperationAdapter(Type dataContextType, bool isCaseSensitive) : base(dataContextType, isCaseSensitive)
        {
        }

        protected override String GetProcedureName(Object dataContext, String operationName, IReadOnlyList<KeyValuePair<String, Object?>> parameters)
        {
            var sql = new StringBuilder("call ");
            sql.Append(operationName);
            if (parameters.Count > 0)
            {
                sql.Append('(');
                String[] parameterNames = GetParameterNames(dataContext, parameters);
                sql.Append(String.Join(",", parameterNames));
                sql.Append(')');
            }
            return sql.ToString();
        }
    }
}
