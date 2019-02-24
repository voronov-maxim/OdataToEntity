using LinqToDB;
using LinqToDB.Data;
using OdataToEntity.Db;
using OdataToEntity.ModelBuilder;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;

namespace OdataToEntity.Linq2Db
{
    public class OeLinq2DbOperationAdapter : OeOperationAdapter
    {
        public OeLinq2DbOperationAdapter(Type dataContextType)
            : base(dataContextType)
        {
        }

        protected override OeAsyncEnumerator ExecuteNonQuery(Object dataContext, String sql, IReadOnlyList<KeyValuePair<String, Object>> parameters)
        {
            var dataConnection = (DataConnection)dataContext;
            dataConnection.Execute(sql, GetDataParameters(parameters));
            return OeAsyncEnumerator.Empty;
        }
        protected override OeAsyncEnumerator ExecuteReader(Object dataContext, String sql, IReadOnlyList<KeyValuePair<String, Object>> parameters, OeEntitySetAdapter entitySetAdapter)
        {
            return ExecuteReader(dataContext, sql, parameters, entitySetAdapter.EntityType);
        }
        private OeAsyncEnumerator ExecuteReader(Object dataContext, String sql, IReadOnlyList<KeyValuePair<String, Object>> parameters, Type retuenType)
        {
            Func<DataConnection, String, DataParameter[], IEnumerable<Object>> queryMethod;
            if (sql.StartsWith("select "))
                queryMethod = DataConnectionExtensions.Query<Object>;
            else
                queryMethod = DataConnectionExtensions.QueryProc<Object>;

            MethodInfo queryMethodInfo = queryMethod.GetMethodInfo().GetGenericMethodDefinition().MakeGenericMethod(new Type[] { retuenType });
            Type queryMethodType = typeof(Func<DataConnection, String, DataParameter[], IEnumerable<Object>>);
            var queryFunc = (Func<DataConnection, String, DataParameter[], IEnumerable<Object>>)Delegate.CreateDelegate(queryMethodType, queryMethodInfo);

            IEnumerable<Object> result = queryFunc((DataConnection)dataContext, sql, GetDataParameters(parameters));
            return OeAsyncEnumerator.Create(result, CancellationToken.None);
        }
        protected override OeAsyncEnumerator ExecutePrimitive(Object dataContext, String sql, IReadOnlyList<KeyValuePair<String, Object>> parameters, Type returnType)
        {
            Type itemType = Parsers.OeExpressionHelper.GetCollectionItemType(returnType);
            if (itemType == null)
            {
                Object result = ((DataConnection)dataContext).Execute<Object>(sql, GetDataParameters(parameters));
                return OeAsyncEnumerator.Create(result, CancellationToken.None);
            }

            return ExecuteReader(dataContext, sql, parameters, itemType);
        }
        private DataParameter[] GetDataParameters(IReadOnlyList<KeyValuePair<String, Object>> parameters)
        {
            var dataParameters = new DataParameter[parameters.Count];
            for (int i = 0; i < dataParameters.Length; i++)
            {
                Object value = GetParameterCore(parameters[i], null, i);
                if (value is DataParameter dataParameter)
                    dataParameters[i] = dataParameter;
                else
                    dataParameters[i] = new DataParameter(parameters[i].Key, value);
            }
            return dataParameters;
        }
        protected override String[] GetParameterNames(Object dataContext, IReadOnlyList<KeyValuePair<String, Object>> parameters)
        {
            var parameterNames = new String[parameters.Count];
            for (int i = 0; i < parameterNames.Length; i++)
                parameterNames[i] = "@" + parameters[i].Key;
            return parameterNames;
        }
        protected override string GetProcedureName(Object dataContext, String operationName, IReadOnlyList<KeyValuePair<String, Object>> parameters)
        {
            return GetOperationCaseSensitivityName(operationName, null);
        }
        protected override IReadOnlyList<OeOperationConfiguration> GetOperationConfigurations(MethodInfo methodInfo)
        {
            var dbFunction = (Sql.FunctionAttribute)methodInfo.GetCustomAttribute(typeof(Sql.FunctionAttribute));
            if (dbFunction == null)
                return base.GetOperationConfigurations(methodInfo);

            String functionName = String.IsNullOrEmpty(dbFunction.Configuration) ? dbFunction.Name : dbFunction.Configuration + "." + dbFunction.Name;
            return new[] { new OeOperationConfiguration(functionName, methodInfo, true) };
        }
    }
}
