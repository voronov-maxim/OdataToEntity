using LinqToDB.Data;
using Microsoft.OData.Edm;
using OdataToEntity.Db;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace OdataToEntity.Linq2Db
{
    public interface IOeLinq2DbDataContext
    {
        OeLinq2DbDataContext DataContext
        {
            get;
            set;
        }
    }

    public sealed class OeLinq2DbDataContext
    {
        private struct ClrTypeEdmSet
        {
            public readonly Type ClrType;
            public readonly IEdmEntitySet EdmSet;

            public ClrTypeEdmSet(Type clrType, IEdmEntitySet edmSet)
            {
                ClrType = clrType;
                EdmSet = edmSet;
            }
        }

        private readonly Dictionary<Type, OeLinq2DbTable> _tables;

        public OeLinq2DbDataContext()
        {
            _tables = new Dictionary<Type, OeLinq2DbTable>();
        }

        private List<ClrTypeEdmSet> GetClrTypeEdmSetList(IEdmModel edmModel, OeEntitySetMetaAdapterCollection entitySetMetaAdapters)
        {
            var clrTypeEdmSetList = new List<ClrTypeEdmSet>();
            foreach (Type entityType in _tables.Keys)
            {
                OeEntitySetMetaAdapter metaAdapter = entitySetMetaAdapters.FindByClrType(entityType);
                IEdmEntitySet entitySet = edmModel.FindDeclaredEntitySet(metaAdapter.EntitySetName);
                clrTypeEdmSetList.Add(new ClrTypeEdmSet(entityType, entitySet));
            }

            var orderedTypes = new List<ClrTypeEdmSet>();
            while (clrTypeEdmSetList.Count > 0)
                for (int i = 0; i < clrTypeEdmSetList.Count; i++)
                    if (IsDependent(clrTypeEdmSetList[i], clrTypeEdmSetList, out PropertyInfo selfRefProperty))
                    {
                        if (selfRefProperty != null)
                            _tables[selfRefProperty.DeclaringType].SelfRefProperty = selfRefProperty;

                        orderedTypes.Add(clrTypeEdmSetList[i]);
                        clrTypeEdmSetList.RemoveAt(i);
                        break;
                    }
            return orderedTypes;
        }
        private static List<PropertyInfo> GetDependentProperties(Type clrType, IEdmNavigationProperty navigationPropery)
        {
            var clrProperties = new List<PropertyInfo>(1);
            foreach (IEdmStructuralProperty edmProperty in navigationPropery.Partner.DependentProperties())
                clrProperties.Add(clrType.GetProperty(edmProperty.Name));
            return clrProperties;
        }
        public OeLinq2DbTable<T> GetTable<T>()
        {
            if (_tables.TryGetValue(typeof(T), out OeLinq2DbTable value))
                return (OeLinq2DbTable<T>)value;

            var table = new OeLinq2DbTable<T>();
            _tables.Add(typeof(T), table);
            return table;
        }
        public OeLinq2DbTable GetTable(Type entityType)
        {
            if (_tables.TryGetValue(entityType, out OeLinq2DbTable table))
                return table;

            throw new InvalidOperationException("Table entity type " + entityType.FullName + " not found");
        }
        private static bool IsDependent(ClrTypeEdmSet clrTypeEdmSet, List<ClrTypeEdmSet> clrTypeEdmSetList, out PropertyInfo selfRefProperty)
        {
            selfRefProperty = null;

            foreach (IEdmNavigationPropertyBinding navigationBinding in clrTypeEdmSet.EdmSet.NavigationPropertyBindings)
            {
                if (!navigationBinding.NavigationProperty.IsPrincipal())
                {
                    if (clrTypeEdmSet.EdmSet == navigationBinding.Target)
                    {
                        IEdmStructuralProperty edmSelfRefProperty = navigationBinding.NavigationProperty.DependentProperties().Single();
                        selfRefProperty = clrTypeEdmSet.ClrType.GetProperty(edmSelfRefProperty.Name);
                    }
                    continue;
                }

                foreach (ClrTypeEdmSet clrTypeEdmSet2 in clrTypeEdmSetList)
                    if (clrTypeEdmSet2.EdmSet == navigationBinding.Target && clrTypeEdmSet.EdmSet != navigationBinding.Target)
                        return false;
            }
            return true;
        }
        public int SaveChanges(IEdmModel edmModel, OeEntitySetMetaAdapterCollection entitySetMetaAdapters, DataConnection dataConnection)
        {
            List<ClrTypeEdmSet> clrTypeEdmSetList = GetClrTypeEdmSetList(edmModel, entitySetMetaAdapters);
            int count = 0;

            for (int i = clrTypeEdmSetList.Count - 1; i >= 0; i--)
            {
                OeLinq2DbTable table = GetTable(clrTypeEdmSetList[i].ClrType);

                count += table.SaveInserted(dataConnection);
                UpdateIdentities(table, clrTypeEdmSetList, i);

                count += table.SaveUpdated(dataConnection);
            }

            for (int i = 0; i < clrTypeEdmSetList.Count; i++)
            {
                OeLinq2DbTable table = GetTable(clrTypeEdmSetList[i].ClrType);
                count += table.SaveDeleted(dataConnection);
            }

            return count;
        }
        private void UpdateIdentities(OeLinq2DbTable table, List<ClrTypeEdmSet> clrTypeEdmSetList, int lastIndex)
        {
            if (table.Identities.Count == 0)
                return;

            foreach (IEdmNavigationPropertyBinding navigationBinding in clrTypeEdmSetList[lastIndex].EdmSet.NavigationPropertyBindings)
                if (navigationBinding.NavigationProperty.IsPrincipal())
                    for (int j = 0; j <= lastIndex; j++)
                        if (clrTypeEdmSetList[j].EdmSet == navigationBinding.Target)
                        {
                            var dependentProperties = GetDependentProperties(clrTypeEdmSetList[j].ClrType, navigationBinding.NavigationProperty);
                            GetTable(clrTypeEdmSetList[j].ClrType).UpdateIdentities(dependentProperties.Single(), table.Identities);
                            break;
                        }
        }
    }
}
