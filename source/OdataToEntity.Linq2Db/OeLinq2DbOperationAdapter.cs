using LinqToDB;
using LinqToDB.Data;
using OdataToEntity.Db;
using OdataToEntity.ModelBuilder;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

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
        protected override OeAsyncEnumerator ExecuteReader(Object dataContext, String sql, IReadOnlyList<KeyValuePair<String, Object>> parameters, Type returnType)
        {
            Func<DataConnection, String, DataParameter[], IEnumerable<Object>> queryMethod;
            if (sql.StartsWith("select "))
                queryMethod = DataConnectionExtensions.Query<Object>;
            else
                queryMethod = DataConnectionExtensions.QueryProc<Object>;

            MethodInfo queryMethodInfo = queryMethod.GetMethodInfo().GetGenericMethodDefinition().MakeGenericMethod(new Type[] { returnType });
            Type queryMethodType = typeof(Func<DataConnection, String, DataParameter[], IEnumerable<Object>>);
            var queryFunc = (Func<DataConnection, String, DataParameter[], IEnumerable<Object>>)Delegate.CreateDelegate(queryMethodType, queryMethodInfo);

            IEnumerable<Object> result = queryFunc((DataConnection)dataContext, sql, GetDataParameters(parameters));
            return new OeAsyncEnumeratorAdapter(result, CancellationToken.None);
        }
        protected override OeAsyncEnumerator ExecuteScalar(Object dataContext, String sql, IReadOnlyList<KeyValuePair<String, Object>> parameters, Type returnType)
        {
            Object result = ((DataConnection)dataContext).Execute<Object>(sql, GetDataParameters(parameters));
            return new OeScalarAsyncEnumeratorAdapter(Task.FromResult(result), CancellationToken.None);
        }
        private static DataParameter[] GetDataParameters(IReadOnlyList<KeyValuePair<String, Object>> parameters)
        {
            var dataParameters = new DataParameter[parameters.Count];
            for (int i = 0; i < dataParameters.Length; i++)
                dataParameters[i] = new DataParameter(parameters[i].Key, parameters[i].Value);
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
        protected override OeOperationConfiguration GetOperationConfiguration(MethodInfo methodInfo)
        {
            var dbFunction = (Sql.FunctionAttribute)methodInfo.GetCustomAttribute(typeof(Sql.FunctionAttribute));
            if (dbFunction == null)
                return base.GetOperationConfiguration(methodInfo);

            String functionName = String.IsNullOrEmpty(dbFunction.Configuration) ? dbFunction.Name : dbFunction.Configuration + "." + dbFunction.Name;
            return new OeOperationConfiguration(functionName, methodInfo, true);
        }
    }
}
