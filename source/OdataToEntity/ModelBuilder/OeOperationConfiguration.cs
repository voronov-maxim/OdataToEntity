using System;
using System.Collections.Generic;
using System.Reflection;

namespace OdataToEntity.ModelBuilder
{
    public sealed class OeOperationConfiguration
    {
        public OeOperationConfiguration(String name, MethodInfo methodInfo, bool? isDbFunction)
        {
            MethodInfo = methodInfo;
            Parameters = GetParameters(methodInfo);

            bool parentheses = name.EndsWith("()");
            ImportName = parentheses ? name.Substring(0, name.Length - 2) : name;
            IsDbFunction = isDbFunction ?? parentheses;
            Name = methodInfo.Name;
        }

        public OeOperationConfiguration(String name, MethodInfo methodInfo, bool isBound, bool isCollection)
        {
            MethodInfo = methodInfo;
            Parameters = GetBoundParameters(methodInfo, isCollection);

            IsBound = isBound;
            Name = name;
        }
        private static OeOperationParameterConfiguration[] GetBoundParameters(MethodInfo methodInfo, bool isCollection)
        {
            ParameterInfo[] parameterInfos = methodInfo.GetParameters();
            Type boundParameterType = parameterInfos[0].ParameterType;
            if (!(boundParameterType.IsGenericType && typeof(Db.OeBoundFunctionParameter).IsAssignableFrom(boundParameterType)))
                throw new InvalidOperationException("First parameter in bound function must be OeBoundFunctionParameter<,>");

            var parameters = new OeOperationParameterConfiguration[parameterInfos.Length];

            Type entityType = boundParameterType.GetGenericArguments()[0];
            Type parameterType = isCollection ? typeof(IEnumerable<>).MakeGenericType(entityType) : entityType;
            parameters[0] = new OeOperationParameterConfiguration(parameterInfos[0].Name, parameterType);
            for (int i = 1; i < parameterInfos.Length; i++)
                parameters[i] = new OeOperationParameterConfiguration(parameterInfos[i].Name, parameterInfos[i].ParameterType);

            return parameters;
        }
        private static OeOperationParameterConfiguration[] GetParameters(MethodInfo methodInfo)
        {
            ParameterInfo[] parameterInfos = methodInfo.GetParameters();
            var parameters = new OeOperationParameterConfiguration[parameterInfos.Length];
            for (int i = 0; i < parameterInfos.Length; i++)
                parameters[i] = new OeOperationParameterConfiguration(parameterInfos[i].Name, parameterInfos[i].ParameterType);
            return parameters;
        }

        public bool IsBound { get; }
        public bool IsDbFunction { get; }
        public bool IsEdmFunction => ReturnType != null && ReturnType != typeof(void);
        public MethodInfo MethodInfo { get; }
        public String Name { get; }
        public String ImportName { get; }
        public String NamespaceName => MethodInfo.DeclaringType.Namespace;
        public OeOperationParameterConfiguration[] Parameters { get; }
        public Type ReturnType => MethodInfo.ReturnType;
    }

    public readonly struct OeOperationParameterConfiguration
    {
        public OeOperationParameterConfiguration(String name, Type clrType)
        {
            Name = name;
            ClrType = clrType;
        }

        public Type ClrType { get; }
        public String Name { get; }
    }
}
