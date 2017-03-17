using System;
using System.Collections.Generic;

namespace OdataToEntity.ModelBuilder
{
    public sealed class OeOperationConfiguration
    {
        private readonly String _name;
        private readonly String _namespaceName;
        private readonly List<OeFunctionParameterConfiguration> _parameters;

        public OeOperationConfiguration(String namespaceName, String name)
        {
            _namespaceName = namespaceName;
            _name = name;
            _parameters = new List<OeFunctionParameterConfiguration>();
        }

        public void AddParameter<T>(String name)
        {
            AddParameter(name, typeof(T));
        }
        public void AddParameter(String name, Type clrType)
        {
            _parameters.Add(new OeFunctionParameterConfiguration(name, clrType));
        }

        public bool IsFunction => ReturnType != null && ReturnType != typeof(void);
        public String Name => _name;
        public String NamespaceName => _namespaceName;
        public IReadOnlyList<OeFunctionParameterConfiguration> Parameters => _parameters;
        public Type ReturnType { get; set; }
    }

    public struct OeFunctionParameterConfiguration
    {
        private readonly Type _clrType;
        private readonly String _name;

        public OeFunctionParameterConfiguration(String name, Type clrType)
        {
            _name = name;
            _clrType = clrType;
        }

        public Type ClrType => _clrType;
        public String Name => _name;
    }
}
