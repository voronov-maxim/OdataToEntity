using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using OdataToEntity.ModelBuilder;
using OdataToEntity.Parsers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity.Db
{
    public abstract class OeOperationAdapter
    {
        protected readonly Type _dataContextType;
        private IReadOnlyList<OeOperationConfiguration> _operations;

        public OeOperationAdapter(Type dataContextType)
        {
            _dataContextType = dataContextType;
        }

        public OeAsyncEnumerator ApplyBoundFunction(OeAsyncEnumerator asyncEnumerator, OeQueryContext queryContext)
        {
            if (queryContext.ODataUri.Path.LastSegment is OperationSegment operationSegment)
            {
                var edmFunction = (IEdmFunction)operationSegment.Operations.First();
                MethodInfo methodInfo = queryContext.EdmModel.GetMethodInfo(edmFunction);

                IReadOnlyList<KeyValuePair<String, Object>> parameterList = OeOperationHelper.GetParameters(
                    queryContext.EdmModel, operationSegment, queryContext.ODataUri.ParameterAliasNodes);

                Object boundParameter;
                Type boundParameterType = methodInfo.GetParameters()[1].ParameterType;
                if (boundParameterType.IsGenericType && boundParameterType.GetGenericTypeDefinition() == typeof(IAsyncEnumerator<>))
                {
                    boundParameterType = boundParameterType.GetGenericArguments()[0];
                    Type asyncEnumeratorAdapterType = typeof(OeAsyncEnumeratorAdapter<>).MakeGenericType(boundParameterType);
                    ConstructorInfo ctor = asyncEnumeratorAdapterType.GetConstructor(new[] { typeof(OeAsyncEnumerator) });
                    boundParameter = ctor.Invoke(new Object[] { asyncEnumerator });
                }
                else
                    boundParameter = asyncEnumerator.MoveNextAsync().GetAwaiter().GetResult() ? asyncEnumerator.Current : null;

                var parameters = new Object[parameterList.Count + 2];
                parameters[0] = queryContext.EdmModel;
                parameters[1] = boundParameter;
                for (int i = 2; i < parameters.Length; i++)
                    parameters[i] = parameterList[i - 2].Value;

                Object result = methodInfo.Invoke(null, parameters);
                if (result is IEnumerable enumerable)
                    return new OeAsyncEnumeratorAdapter(enumerable, asyncEnumerator.CancellationToken);
                else
                    return new OeScalarAsyncEnumeratorAdapter(Task.FromResult(result), asyncEnumerator.CancellationToken);
            }

            return asyncEnumerator;
        }
        public virtual OeAsyncEnumerator ExecuteFunctionNonQuery(Object dataContext, String operationName, IReadOnlyList<KeyValuePair<String, Object>> parameters)
        {
            String sql = GetSql(dataContext, parameters);
            String functionName = GetOperationCaseSensitivityName(operationName, GetDefaultSchema(dataContext));
            return ExecuteNonQuery(dataContext, "select " + functionName + sql.ToString(), parameters);
        }
        public virtual OeAsyncEnumerator ExecuteFunctionPrimitive(Object dataContext, String operationName, IReadOnlyList<KeyValuePair<String, Object>> parameters, Type returnType)
        {
            String sql = GetSql(dataContext, parameters);
            String functionName = GetOperationCaseSensitivityName(operationName, GetDefaultSchema(dataContext));
            String selectSql = (Parsers.OeExpressionHelper.GetCollectionItemType(returnType) == null ? "select " : "select * from ") + functionName + sql.ToString();
            return ExecutePrimitive(dataContext, selectSql, parameters, returnType);
        }
        public virtual OeAsyncEnumerator ExecuteFunctionReader(Object dataContext, String operationName, IReadOnlyList<KeyValuePair<String, Object>> parameters, OeEntitySetAdapter entitySetAdapter)
        {
            String sql = GetSql(dataContext, parameters);
            String functionName = GetOperationCaseSensitivityName(operationName, GetDefaultSchema(dataContext));
            return ExecuteReader(dataContext, "select * from " + functionName + sql.ToString(), parameters, entitySetAdapter);
        }
        protected abstract OeAsyncEnumerator ExecuteNonQuery(Object dataContext, String sql, IReadOnlyList<KeyValuePair<String, Object>> parameters);
        public virtual OeAsyncEnumerator ExecuteProcedureNonQuery(Object dataContext, String operationName, IReadOnlyList<KeyValuePair<String, Object>> parameters)
        {
            String procedureName = GetProcedureName(dataContext, operationName, parameters);
            return ExecuteNonQuery(dataContext, procedureName, parameters);
        }
        public virtual OeAsyncEnumerator ExecuteProcedurePrimitive(Object dataContext, String operationName, IReadOnlyList<KeyValuePair<String, Object>> parameters, Type returnType)
        {
            String procedureName = GetProcedureName(dataContext, operationName, parameters);
            return ExecutePrimitive(dataContext, procedureName, parameters, returnType);
        }
        public virtual OeAsyncEnumerator ExecuteProcedureReader(Object dataContext, String operationName, IReadOnlyList<KeyValuePair<String, Object>> parameters, OeEntitySetAdapter entitySetAdapter)
        {
            String procedureName = GetProcedureName(dataContext, operationName, parameters);
            return ExecuteReader(dataContext, procedureName, parameters, entitySetAdapter);
        }
        protected abstract OeAsyncEnumerator ExecuteReader(Object dataContext, String sql, IReadOnlyList<KeyValuePair<String, Object>> parameters, OeEntitySetAdapter entitySetAdapter);
        protected abstract OeAsyncEnumerator ExecutePrimitive(Object dataContext, String sql, IReadOnlyList<KeyValuePair<String, Object>> parameters, Type returnType);
        private static String GetCaseSensitivityName(String name) => name[0] == '"' ? name : "\"" + name + "\"";
        protected virtual String GetDefaultSchema(Object dataContext) => null;
        protected IReadOnlyList<MethodInfo> GetMethodInfos()
        {
            var methodInfos = new List<MethodInfo>();
            foreach (MethodInfo methodInfo in _dataContextType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                if (!methodInfo.IsSpecialName)
                {
                    if (methodInfo.IsVirtual || methodInfo.IsGenericMethod || methodInfo.GetBaseDefinition().DeclaringType != _dataContextType)
                        continue;

                    methodInfos.Add(methodInfo);
                }
            return methodInfos;
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
        public IReadOnlyList<OeOperationConfiguration> GetOperations()
        {
            if (_operations == null)
            {
                IReadOnlyList<OeOperationConfiguration> operations = GetOperationsCore(_dataContextType);
                Interlocked.CompareExchange(ref _operations, operations, null);
            }

            return _operations;
        }
        protected virtual OeOperationConfiguration GetOperationConfiguration(MethodInfo methodInfo)
        {
            var description = (DescriptionAttribute)methodInfo.GetCustomAttribute(typeof(DescriptionAttribute));
            return description == null ? null : new OeOperationConfiguration(description.Description, methodInfo, null);
        }
        protected virtual IReadOnlyList<OeOperationConfiguration> GetOperationsCore(Type dataContextType)
        {
            IReadOnlyList<MethodInfo> methodInfos = GetMethodInfos();
            var operations = new List<OeOperationConfiguration>(methodInfos.Count);
            for (int i = 0; i < methodInfos.Count; i++)
            {
                OeOperationConfiguration operationConfiguration = GetOperationConfiguration(methodInfos[i]);
                if (operationConfiguration != null)
                    operations.Add(operationConfiguration);
            }
            return operations;
        }
        protected virtual Object GetParameterCore(KeyValuePair<String, Object> parameter, String parameterName, int parameterIndex)
        {
            return parameter.Value;
        }
        protected abstract String[] GetParameterNames(Object dataContext, IReadOnlyList<KeyValuePair<String, Object>> parameters);
        protected Object[] GetParameterValues(IReadOnlyList<KeyValuePair<String, Object>> parameters)
        {
            if (parameters.Count == 0)
                return Array.Empty<Object>();

            var parameterValues = new Object[parameters.Count];
            for (int i = 0; i < parameterValues.Length; i++)
                parameterValues[i] = GetParameterCore(parameters[i], null, i);
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
