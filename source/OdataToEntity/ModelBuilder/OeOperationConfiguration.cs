using System;
using System.Collections.Generic;
using System.Reflection;

namespace OdataToEntity.ModelBuilder
{
    public sealed class OeOperationConfiguration
    {
        private OeOperationConfiguration(String? schema, String name, String namespaceName, OeOperationParameterConfiguration[] parameters, Type returnType)
        {
            Schema = schema;
            NamespaceName = namespaceName;
            Name = name;
            Parameters = parameters;
            ReturnType = returnType;
        }
        public OeOperationConfiguration(String? schema, String name, MethodInfo methodInfo, bool? isDbFunction)
            : this(schema, GetName(name), methodInfo.DeclaringType!.Namespace ?? "", GetParameters(methodInfo), methodInfo.ReturnType)
        {
            ImportName = schema == null ? Name : schema + "." + Name;
            IsDbFunction = isDbFunction ?? name.EndsWith("()", StringComparison.Ordinal);
            MethodInfo = methodInfo;
        }
        public OeOperationConfiguration(String? schema, String name, MethodInfo methodInfo, bool isBound, bool isCollection)
            : this(schema, name, methodInfo.DeclaringType!.Namespace ?? "", GetBoundParameters(methodInfo, isCollection), methodInfo.ReturnType)
        {
            IsBound = isBound;
            MethodInfo = methodInfo;
        }
        public OeOperationConfiguration(String schema, String name, String namespaceName, OeOperationParameterConfiguration[] parameters, Type returnType, bool isDbFunction)
            : this(schema, name, namespaceName, parameters, returnType)
        {
            ImportName = String.IsNullOrEmpty(schema) ? name : schema + "." + name;
            IsDbFunction = isDbFunction;
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
            parameters[0] = new OeOperationParameterConfiguration(parameterInfos[0].Name!, parameterType);
            for (int i = 1; i < parameterInfos.Length; i++)
                parameters[i] = new OeOperationParameterConfiguration(parameterInfos[i].Name!, parameterInfos[i].ParameterType);

            return parameters;
        }
        private static String GetName(String name)
        {
            return name.EndsWith("()", StringComparison.Ordinal) ? name.Substring(0, name.Length - 2) : name;
        }
        private static OeOperationParameterConfiguration[] GetParameters(MethodInfo methodInfo)
        {
            ParameterInfo[] parameterInfos = methodInfo.GetParameters();
            var parameters = new OeOperationParameterConfiguration[parameterInfos.Length];
            for (int i = 0; i < parameterInfos.Length; i++)
                parameters[i] = new OeOperationParameterConfiguration(parameterInfos[i].Name!, parameterInfos[i].ParameterType);
            return parameters;
        }

        public bool IsBound { get; }
        public bool IsDbFunction { get; }
        public MethodInfo? MethodInfo { get; }
        public String Name { get; }
        public String? ImportName { get; }
        public String NamespaceName { get; }
        public OeOperationParameterConfiguration[] Parameters { get; }
        public Type ReturnType { get; }
        public String? Schema { get; }
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
