using Microsoft.OData.Edm;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace OdataToEntity.ModelBuilder
{
    public sealed class OeOperationConfiguration
    {
        private readonly bool _isBound;
        private readonly List<OeOperationParameterConfiguration> _parameters;

        public OeOperationConfiguration(String name, MethodInfo methodInfo, bool? isDbFunction)
        {
            MethodInfo = methodInfo;
            _parameters = GetParameters(methodInfo, out _isBound);

            bool parentheses = name.EndsWith("()");
            ImportName = parentheses ? name.Substring(0, name.Length - 2) : name;
            IsDbFunction = isDbFunction ?? parentheses;
        }

        private static List<OeOperationParameterConfiguration> GetParameters(MethodInfo methodInfo, out bool isBound)
        {
            isBound = false;

            ParameterInfo[] parameterInfos = methodInfo.GetParameters();
            var parameters = new List<OeOperationParameterConfiguration>(parameterInfos.Length);
            for (int i = 0; i < parameterInfos.Length; i++)
                if (i == 0 && parameterInfos[0].ParameterType == typeof(IEdmModel))
                {
                    bool isCollection;
                    Type entityType = parameterInfos[1].ParameterType;
                    if (isCollection = entityType.IsGenericType && entityType.GetGenericTypeDefinition() == typeof(IAsyncEnumerator<>))
                        entityType = entityType.GetGenericArguments()[0];

                    if (!Parsers.OeExpressionHelper.IsEntityType(entityType))
                        throw new InvalidOperationException("Second parameter in function " + methodInfo.Name + " must be entity or IAsyncEnumerable<entity>");

                    isBound = true;
                    Type parameterType = isCollection ? typeof(IEnumerable<>).MakeGenericType(entityType) : entityType;
                    parameters.Add(new OeOperationParameterConfiguration(parameterInfos[i].Name, parameterType));

                    i = 1;
                }
                else
                    parameters.Add(new OeOperationParameterConfiguration(parameterInfos[i].Name, parameterInfos[i].ParameterType));

            return parameters;
        }

        public bool IsBound => _isBound;
        public bool IsDbFunction { get; }
        public bool IsEdmFunction => ReturnType != null && ReturnType != typeof(void);
        public MethodInfo MethodInfo { get; }
        public String Name => MethodInfo.Name;
        public String ImportName { get; }
        public String NamespaceName => MethodInfo.DeclaringType.Namespace;
        public IReadOnlyList<OeOperationParameterConfiguration> Parameters => _parameters;
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
