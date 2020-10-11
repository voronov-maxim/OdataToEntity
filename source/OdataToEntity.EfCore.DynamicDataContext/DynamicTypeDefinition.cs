using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Collections.Generic;

namespace OdataToEntity.EfCore.DynamicDataContext
{
    public sealed class DynamicTypeDefinition
    {
        private readonly Dictionary<INavigation, Type> _navigations;

        public DynamicTypeDefinition(Type dynamicTypeType, String entityName, String tableEdmName, bool isQueryType)
        {
            DynamicTypeType = dynamicTypeType;
            EntityName = entityName;
            TableEdmName = tableEdmName;
            IsQueryType = isQueryType;

            _navigations = new Dictionary<INavigation, Type>();
        }

        internal void AddNavigationProperty(INavigation navigation, Type clrType)
        {
            if (!_navigations.TryGetValue(navigation, out _))
                _navigations.Add(navigation, clrType);
        }
        public Type GetNavigationPropertyClrType(INavigation navigation)
        {
            return _navigations[navigation];
        }

        public Type DynamicTypeType { get; }
        public String EntityName { get; }
        public bool IsQueryType { get; }
        public String TableEdmName { get; }
    }
}
