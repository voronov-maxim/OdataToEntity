using OdataToEntity.ModelBuilder;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
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

        public abstract OeAsyncEnumerator ExecuteFunction(Object dataContext, String operationName, IReadOnlyList<KeyValuePair<String, Object>> parameters, Type returnType);
        public abstract OeAsyncEnumerator ExecuteProcedure(Object dataContext, String operationName, IReadOnlyList<KeyValuePair<String, Object>> parameters, Type returnType);
        protected MethodInfo[] GetMethodInfos()
        {
            var methodInfos = new List<MethodInfo>();
            foreach (MethodInfo methodInfo in _dataContextType.GetTypeInfo().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                if (!methodInfo.IsSpecialName)
                {
                    if (methodInfo.IsVirtual || methodInfo.IsGenericMethod || methodInfo.GetBaseDefinition().DeclaringType != _dataContextType)
                        continue;
                    methodInfos.Add(methodInfo);
                }
            return methodInfos.ToArray();
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
            var operation = new OeOperationConfiguration(name, null);
            foreach (ParameterInfo parameterInfo in methodInfo.GetParameters())
                operation.AddParameter(parameterInfo.Name, parameterInfo.ParameterType);
            operation.ReturnType = methodInfo.ReturnType;
            return operation;
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
    }
}
