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

        protected override IAsyncEnumerable<Object> ExecuteNonQuery(Object dataContext, String sql, IReadOnlyList<KeyValuePair<String, Object>> parameters)
        {
            ((DbContext)dataContext).Database.ExecuteSqlCommand(sql, GetParameterValues(parameters));
            return Infrastructure.AsyncEnumeratorHelper.Empty;
        }
        protected override IAsyncEnumerable<Object> ExecuteReader(Object dataContext, String sql, IReadOnlyList<KeyValuePair<String, Object>> parameters, OeEntitySetAdapter entitySetAdapter)
        {
            DbRawSqlQuery query = ((DbContext)dataContext).Database.SqlQuery(entitySetAdapter.EntityType, sql, GetParameterValues(parameters));
            return Infrastructure.AsyncEnumeratorHelper.ToAsyncEnumerable(query);
        }
        protected override IAsyncEnumerable<Object> ExecutePrimitive(Object dataContext, String sql,
            IReadOnlyList<KeyValuePair<String, Object>> parameters, Type returnType, CancellationToken cancellationToken)
        {
            returnType = Parsers.OeExpressionHelper.GetCollectionItemTypeOrNull(returnType) ?? returnType;
            DbRawSqlQuery query = ((DbContext)dataContext).Database.SqlQuery(returnType, sql, GetParameterValues(parameters));
            return Infrastructure.AsyncEnumeratorHelper.ToAsyncEnumerable(query);
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
        protected override IReadOnlyList<OeOperationConfiguration> GetOperationConfigurations(MethodInfo methodInfo)
        {
            var dbFunction = (DbFunctionAttribute)methodInfo.GetCustomAttribute(typeof(DbFunctionAttribute));
            if (dbFunction == null)
                return base.GetOperationConfigurations(methodInfo);

            String functionName = dbFunction.FunctionName ?? methodInfo.Name;
            String schema = !String.IsNullOrEmpty(dbFunction.NamespaceName) && dbFunction.NamespaceName != "." ? dbFunction.NamespaceName : null;
            return new[] { new OeOperationConfiguration(schema, functionName, methodInfo, true) };
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
