using OdataToEntity.Db;
using OdataToEntity.ModelBuilder;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Reflection;
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

        protected override OeAsyncEnumerator ExecuteNonQuery(Object dataContext, String sql, IReadOnlyList<KeyValuePair<String, Object>> parameters)
        {
            ((DbContext)dataContext).Database.ExecuteSqlCommand(sql, GetParameterValues(parameters));
            return OeAsyncEnumerator.Empty;
        }
        protected override OeAsyncEnumerator ExecuteReader(Object dataContext, String sql, IReadOnlyList<KeyValuePair<String, Object>> parameters, OeEntitySetAdapter entitySetAdapter)
        {
            DbRawSqlQuery query = ((DbContext)dataContext).Database.SqlQuery(entitySetAdapter.EntityType, sql, GetParameterValues(parameters));
            return new OeAsyncEnumeratorAdapter(query, CancellationToken.None);
        }
        protected override OeAsyncEnumerator ExecuteScalar(Object dataContext, String sql, IReadOnlyList<KeyValuePair<String, Object>> parameters, Type returnType)
        {
            DbRawSqlQuery query = ((DbContext)dataContext).Database.SqlQuery(returnType, sql, GetParameterValues(parameters));
            return new OeAsyncEnumeratorAdapter(query, CancellationToken.None);
        }
        private static String GetDbParameterName(DbContext dbContext, int parameterOrder)
        {
            DummyCommandBuilder dummyCommandBuilder = Volatile.Read(ref _dummyCommandBuilder);
            if (dummyCommandBuilder == null)
            {
                dummyCommandBuilder = new DummyCommandBuilder(dbContext.Database.Connection);
                Volatile.Write(ref _dummyCommandBuilder, dummyCommandBuilder);
            }
            return dummyCommandBuilder.GetDbParameterName(parameterOrder);
        }
        protected override String GetDefaultSchema(Object dataContext) => null;
        protected override OeOperationConfiguration GetOperationConfiguration(MethodInfo methodInfo)
        {
            var dbFunction = (DbFunctionAttribute)methodInfo.GetCustomAttribute(typeof(DbFunctionAttribute));
            if (dbFunction == null)
                return base.GetOperationConfiguration(methodInfo);

            String functionName = dbFunction.FunctionName ?? methodInfo.Name;
            if (!String.IsNullOrEmpty(dbFunction.NamespaceName) && dbFunction.NamespaceName != ".")
                functionName = dbFunction.NamespaceName + "." + functionName;

            return new OeOperationConfiguration(functionName, methodInfo, true);
        }
        protected override String[] GetParameterNames(Object dataContext, IReadOnlyList<KeyValuePair<String, Object>> parameters)
        {
            var dbContext = (DbContext)dataContext;
            var parameterNames = new String[parameters.Count];
            for (int i = 0; i < parameterNames.Length; i++)
                parameterNames[i] = GetDbParameterName(dbContext, i);
            return parameterNames;
        }
    }
}
