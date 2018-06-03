using System;
using System.Collections.Generic;
using System.Reflection;

namespace OdataToEntity.ModelBuilder
{
    public sealed class OeOperationConfiguration
    {
        private readonly List<OeOperationParameterConfiguration> _parameters;

        public OeOperationConfiguration(String name, MethodInfo methodInfo, bool? isDbFunction)
            : this(name, methodInfo.DeclaringType.Namespace, methodInfo.DeclaringType.Name + "." + methodInfo.Name, methodInfo.ReturnType, isDbFunction)
        {
            foreach (ParameterInfo parameterInfo in methodInfo.GetParameters())
                AddParameter(parameterInfo.Name, parameterInfo.ParameterType);
        }
        public OeOperationConfiguration(String name, String namespaceName, String methodInfoName, Type returnType, bool? isDbFunction)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));

            bool parentheses = name.EndsWith("()");

            Name = parentheses ? name.Substring(0, name.Length - 2) : name;
            NamespaceName = namespaceName;
            MethodInfoName = methodInfoName;
            ReturnType = returnType;
            IsDbFunction = isDbFunction ?? (ReturnType != null && ReturnType.IsPrimitive) || parentheses;

            _parameters = new List<OeOperationParameterConfiguration>();
        }

        public void AddParameter<T>(String name)
        {
            AddParameter(name, typeof(T));
        }
        public void AddParameter(String name, Type clrType)
        {
            _parameters.Add(new OeOperationParameterConfiguration(name, clrType));
        }

        public bool IsDbFunction { get; }
        public bool IsEdmFunction => ReturnType != null && ReturnType != typeof(void);
        public String MethodInfoName { get; }
        public String Name { get; }
        public String NamespaceName { get; }
        public IReadOnlyList<OeOperationParameterConfiguration> Parameters => _parameters;
        public Type ReturnType { get; }
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
