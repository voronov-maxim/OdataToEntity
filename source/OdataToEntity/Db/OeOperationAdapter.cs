using OdataToEntity.ModelBuilder;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Text;
using System.Threading;

namespace OdataToEntity.Db
{
    public abstract class OeOperationAdapter
    {
        protected readonly Type _dataContextType;
        private OeOperationConfiguration[] _operations;

        public OeOperationAdapter(Type dataContextType)
        {
            _dataContextType = dataContextType;
        }

        public virtual OeAsyncEnumerator ExecuteFunctionNonQuery(Object dataContext, String operationName, IReadOnlyList<KeyValuePair<String, Object>> parameters)
        {
            String sql = GetSql(dataContext, parameters);
            String functionName = GetOperationCaseSensitivityName(operationName, GetDefaultSchema(dataContext));
            return ExecuteNonQuery(dataContext, "select " + functionName + sql.ToString(), parameters);
        }
        public virtual OeAsyncEnumerator ExecuteFunctionReader(Object dataContext, String operationName, IReadOnlyList<KeyValuePair<String, Object>> parameters, OeEntitySetAdapter entitySetAdapter)
        {
            String sql = GetSql(dataContext, parameters);
            String functionName = GetOperationCaseSensitivityName(operationName, GetDefaultSchema(dataContext));
            return ExecuteReader(dataContext, "select * from " + functionName + sql.ToString(), parameters, entitySetAdapter);
        }
        public virtual OeAsyncEnumerator ExecuteFunctionScalar(Object dataContext, String operationName, IReadOnlyList<KeyValuePair<String, Object>> parameters, Type returnType)
        {
            String sql = GetSql(dataContext, parameters);
            String functionName = GetOperationCaseSensitivityName(operationName, GetDefaultSchema(dataContext));
            return ExecuteScalar(dataContext, "select " + functionName + sql.ToString(), parameters, returnType);
        }
        protected abstract OeAsyncEnumerator ExecuteNonQuery(Object dataContext, String sql, IReadOnlyList<KeyValuePair<String, Object>> parameters);
        public virtual OeAsyncEnumerator ExecuteProcedureNonQuery(Object dataContext, String operationName, IReadOnlyList<KeyValuePair<String, Object>> parameters)
        {
            String procedureName = GetProcedureName(dataContext, operationName, parameters);
            return ExecuteNonQuery(dataContext, procedureName, parameters);
        }
        public virtual OeAsyncEnumerator ExecuteProcedureReader(Object dataContext, String operationName, IReadOnlyList<KeyValuePair<String, Object>> parameters, OeEntitySetAdapter entitySetAdapter)
        {
            String procedureName = GetProcedureName(dataContext, operationName, parameters);
            return ExecuteReader(dataContext, procedureName, parameters, entitySetAdapter);
        }
        public virtual OeAsyncEnumerator ExecuteProcedureScalar(Object dataContext, String operationName, IReadOnlyList<KeyValuePair<String, Object>> parameters, Type returnType)
        {
            String procedureName = GetProcedureName(dataContext, operationName, parameters);
            return ExecuteScalar(dataContext, procedureName, parameters, returnType);
        }
        protected abstract OeAsyncEnumerator ExecuteReader(Object dataContext, String sql, IReadOnlyList<KeyValuePair<String, Object>> parameters, OeEntitySetAdapter entitySetAdapter);
        protected abstract OeAsyncEnumerator ExecuteScalar(Object dataContext, String sql, IReadOnlyList<KeyValuePair<String, Object>> parameters, Type returnType);
        private static String GetCaseSensitivityName(String name) => name[0] == '"' ? name : "\"" + name + "\"";
        protected virtual String GetDefaultSchema(Object dataContext) => null;
        protected MethodInfo[] GetMethodInfos()
        {
            var methodInfos = new List<MethodInfo>();
            foreach (MethodInfo methodInfo in _dataContextType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                if (!methodInfo.IsSpecialName)
                {
                    if (methodInfo.IsVirtual || methodInfo.IsGenericMethod || methodInfo.GetBaseDefinition().DeclaringType != _dataContextType)
                        continue;
                    methodInfos.Add(methodInfo);
                }
            return methodInfos.ToArray();
        }
        protected static String GetOperationCaseSensitivityName(String operationName, String defaultSchema)
        {
            int i = operationName.IndexOf('.');
            if (i == -1)
            {
                if (String.IsNullOrEmpty(defaultSchema))
                    return GetCaseSensitivityName(operationName);

                return GetCaseSensitivityName(defaultSchema) + "." + GetCaseSensitivityName(operationName);
            }

            if (operationName[0] == '"' && operationName[i + 1] == '"')
                return operationName;

            return GetCaseSensitivityName(operationName.Substring(0, i)) + "." + GetCaseSensitivityName(operationName.Substring(i + 1));
        }
        public OeOperationConfiguration[] GetOperations()
        {
            if (_operations == null)
            {
                OeOperationConfiguration[] operations = GetOperationsCore(_dataContextType);
                Interlocked.CompareExchange(ref _operations, operations, null);
            }

            return _operations;
        }
        protected virtual OeOperationConfiguration GetOperationConfiguration(MethodInfo methodInfo)
        {
            var description = (DescriptionAttribute)methodInfo.GetCustomAttribute(typeof(DescriptionAttribute));
            String name = description == null ? methodInfo.Name : description.Description;
            return new OeOperationConfiguration(name, methodInfo, null);
        }
        protected virtual OeOperationConfiguration[] GetOperationsCore(Type dataContextType)
        {
            MethodInfo[] methodInfos = GetMethodInfos();
            if (methodInfos == null)
                return Array.Empty<OeOperationConfiguration>();

            var operations = new OeOperationConfiguration[methodInfos.Length];
            for (int i = 0; i < methodInfos.Length; i++)
                operations[i] = GetOperationConfiguration(methodInfos[i]);
            return operations;
        }
        protected abstract String[] GetParameterNames(Object dataContext, IReadOnlyList<KeyValuePair<String, Object>> parameters);
        protected static Object[] GetParameterValues(IReadOnlyList<KeyValuePair<String, Object>> parameters)
        {
            if (parameters.Count == 0)
                return Array.Empty<Object>();

            var parameterValues = new Object[parameters.Count];
            for (int i = 0; i < parameterValues.Length; i++)
                parameterValues[i] = parameters[i].Value;
            return parameterValues;
        }
        protected virtual String GetProcedureName(Object dataContext, String operationName, IReadOnlyList<KeyValuePair<String, Object>> parameters)
        {
            var sql = new StringBuilder(GetOperationCaseSensitivityName(operationName, GetDefaultSchema(dataContext)));
            sql.Append(' ');
            String[] parameterNames = GetParameterNames(dataContext, parameters);
            sql.Append(String.Join(",", parameterNames));
            return sql.ToString();
        }
        private String GetSql(Object dataContext, IReadOnlyList<KeyValuePair<String, Object>> parameters)
        {
            var sql = new StringBuilder();
            sql.Append('(');
            String[] parameterNames = GetParameterNames(dataContext, parameters);
            sql.Append(String.Join(",", parameterNames));
            sql.Append(')');

            return sql.ToString();
        }
    }
}
