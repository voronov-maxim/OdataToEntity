using System;
using System.Collections.Generic;
using System.Reflection;

namespace OdataToEntity.ModelBuilder
{
    public sealed class OeOperationConfiguration
    {
        private readonly bool? _isDbFunction;
        private readonly String _methodInfoName;
        private readonly String _name;
        private readonly String _namespaceName;
        private readonly List<OeOperationParameterConfiguration> _parameters;
        private readonly Type _returnType;

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

            _name = name;
            _namespaceName = namespaceName;
            _methodInfoName = methodInfoName;
            _returnType = returnType;
            _isDbFunction = isDbFunction;

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

        public bool IsDbFunction => _isDbFunction ?? (ReturnType != null && ReturnType.IsPrimitive);
        public bool IsEdmFunction => ReturnType != null && ReturnType != typeof(void);
        public String MethodInfoName => _methodInfoName;
        public String Name => _name;
        public String NamespaceName => _namespaceName;
        public IReadOnlyList<OeOperationParameterConfiguration> Parameters => _parameters;
        public Type ReturnType => _returnType;
    }

    public struct OeOperationParameterConfiguration
    {
        private readonly Type _clrType;
        private readonly String _name;

        public OeOperationParameterConfiguration(String name, Type clrType)
        {
            _name = name;
            _clrType = clrType;
        }

        public Type ClrType => _clrType;
        public String Name => _name;
    }
}
