using OdataToEntity.Db;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Text;
using System.Threading;

namespace OdataToEntity.Ef6
{
    public class OeEf6OperationAdapter : OeOperationAdapter
    {
        private static DummyCommandBuilder _dummyCommandBuilder;

        public OeEf6OperationAdapter(Type dataContextType)
            : base(dataContextType)
        {
        }

        public override OeAsyncEnumerator ExecuteFunction(object dataContext, string operationName, IReadOnlyList<KeyValuePair<string, object>> parameters, Type returnType)
        {
            throw new NotImplementedException();
        }
        public override OeAsyncEnumerator ExecuteProcedure(object dataContext, string operationName, IReadOnlyList<KeyValuePair<string, object>> parameters, Type returnType)
        {
            var dbContext = (DbContext)dataContext;

            var sql = new StringBuilder(operationName);
            for (int i = 0; i < parameters.Count; i++)
            {
                if (i == 0)
                    sql.Append(' ');

                sql.Append(GetDbParameterName(dbContext, i));
                if (i < parameters.Count - 1)
                    sql.Append(',');
            }

            Object[] parameterValues = Array.Empty<Object>();
            if (parameters.Count > 0)
            {
                parameterValues = new Object[parameters.Count];
                for (int i = 0; i < parameterValues.Length; i++)
                    parameterValues[i] = parameters[i].Value;
            }

            if (returnType == null)
            {
                int count = dbContext.Database.ExecuteSqlCommand(sql.ToString(), parameterValues);
                return new OeAsyncEnumeratorAdapter(new Object[] { count }, CancellationToken.None);
            }
            else
            {
                DbRawSqlQuery query = dbContext.Database.SqlQuery(returnType, sql.ToString(), parameterValues);
                return new OeAsyncEnumeratorAdapter(query, CancellationToken.None);
            }
        }
        private static String GetDbParameterName(DbContext dbContext, int parameterOrder)
        {
            if (_dummyCommandBuilder == null)
                Volatile.Write(ref _dummyCommandBuilder, new DummyCommandBuilder(dbContext.Database.Connection));
            return _dummyCommandBuilder.GetDbParameterName(parameterOrder);
        }
    }
}
