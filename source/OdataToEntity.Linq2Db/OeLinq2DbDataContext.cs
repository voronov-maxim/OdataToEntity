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
        OeLinq2DbDataContext DataContext { get; set; }
    }

    public sealed class OeLinq2DbDataContext
    {
        private readonly struct ClrTableTypeEdmSet
        {
            public ClrTableTypeEdmSet(Type clrTableType, IEdmEntitySet edmSet)
            {
                ClrTableType = clrTableType;
                EdmEntitySet = edmSet;
            }

            public Type ClrTableType { get; }
            public IEdmEntitySet EdmEntitySet { get; }
        }

        private static ClrTableTypeEdmSet[] _orderedTableTypes;
        private readonly Dictionary<Type, OeLinq2DbTable> _tables;

        public OeLinq2DbDataContext(IEdmModel edmModel, OeEntitySetAdapterCollection entitySetAdapters)
        {
            if (_orderedTableTypes == null)
                _orderedTableTypes = GetOrderedTableTypes(edmModel, entitySetAdapters);

            _tables = new Dictionary<Type, OeLinq2DbTable>(_orderedTableTypes.Length);
        }

        private static List<PropertyInfo> GetDependentProperties(Type clrType, IEdmNavigationProperty navigationProperty)
        {
            IEnumerable<IEdmStructuralProperty> edmProperties;
            if (navigationProperty.IsPrincipal())
                edmProperties = navigationProperty.Partner.DependentProperties();
            else
            {
                if (navigationProperty.Type.IsCollection())
                    edmProperties = navigationProperty.DependentProperties();
                else
                    edmProperties = navigationProperty.PrincipalProperties();
            }

            var clrProperties = new List<PropertyInfo>();
            foreach (IEdmStructuralProperty keyProperty in edmProperties)
                clrProperties.Add(clrType.GetPropertyIgnoreCase(keyProperty.Name));
            return clrProperties;
        }
        private static List<PropertyInfo> GetDependentProperties(PropertyInfo propertyInfo, ClrTableTypeEdmSet[] clrTypeEdmSets, int lastIndex)
        {
            var dependentProperties = new List<PropertyInfo>();

            for (int i = 0; i < lastIndex; i++)
                foreach (IEdmNavigationPropertyBinding navigationBinding in clrTypeEdmSets[i].EdmEntitySet.NavigationPropertyBindings)
                {
                    IEdmReferentialConstraint referentialConstraint = navigationBinding.NavigationProperty.ReferentialConstraint;
                    if (referentialConstraint != null)
                        foreach (EdmReferentialConstraintPropertyPair propertyPair in referentialConstraint.PropertyPairs)
                        {
                            var schemaElement = (IEdmSchemaElement)propertyPair.PrincipalProperty.DeclaringType;
                            if (propertyInfo.Name == propertyPair.PrincipalProperty.Name &&
                                propertyInfo.DeclaringType.Name == schemaElement.Name &&
                                propertyInfo.DeclaringType.Namespace == schemaElement.Namespace)
                            {
                                dependentProperties.Add(clrTypeEdmSets[i].ClrTableType.GetProperty(propertyPair.DependentProperty.Name));
                                break;
                            }
                        }
                }

            return dependentProperties;
        }
        private static ClrTableTypeEdmSet[] GetOrderedTableTypes(IEdmModel edmModel, OeEntitySetAdapterCollection entitySetAdapters)
        {
            var clrTypeEdmSetList = new List<ClrTableTypeEdmSet>();
            foreach (OeEntitySetAdapter entitySetAdapter in entitySetAdapters)
            {
                IEdmEntitySet entitySet = OeEdmClrHelper.GetEntitySet(edmModel, entitySetAdapter.EntitySetName);
                clrTypeEdmSetList.Add(new ClrTableTypeEdmSet(entitySetAdapter.EntityType, entitySet));
            }

            var orderedTableTypeList = new List<ClrTableTypeEdmSet>();
            while (clrTypeEdmSetList.Count > 0)
                for (int i = 0; i < clrTypeEdmSetList.Count; i++)
                    if (IsDependent(clrTypeEdmSetList[i], clrTypeEdmSetList, out PropertyInfo selfRefProperty))
                    {
                        Type linq2DbTableType = typeof(OeLinq2DbTable<>).MakeGenericType(clrTypeEdmSetList[i].ClrTableType);
                        if (selfRefProperty != null)
                            linq2DbTableType.GetProperty(nameof(OeLinq2DbTable<Object>.SelfRefProperty)).SetValue(null, selfRefProperty);

                        orderedTableTypeList.Add(clrTypeEdmSetList[i]);
                        clrTypeEdmSetList.RemoveAt(i);
                        break;
                    }
            return orderedTableTypeList.ToArray();
        }
        public OeLinq2DbTable GetTable(Type entityType)
        {
            if (_tables.TryGetValue(entityType, out OeLinq2DbTable table))
                return table;

            return null;
        }
        internal OeLinq2DbTable<T> GetTable<T>() where T : class
        {
            if (_tables.TryGetValue(typeof(T), out OeLinq2DbTable value))
                return (OeLinq2DbTable<T>)value;

            var table = new OeLinq2DbTable<T>();
            _tables.Add(typeof(T), table);
            return table;
        }
        private static bool IsDependent(ClrTableTypeEdmSet clrTypeEdmSet, List<ClrTableTypeEdmSet> clrTypeEdmSetList, out PropertyInfo selfRefProperty)
        {
            selfRefProperty = null;
            foreach (IEdmNavigationPropertyBinding navigationBinding in clrTypeEdmSet.EdmEntitySet.NavigationPropertyBindings)
            {
                if (navigationBinding.NavigationProperty.IsPrincipal() || navigationBinding.NavigationProperty.Partner == null)
                {
                    foreach (ClrTableTypeEdmSet clrTypeEdmSet2 in clrTypeEdmSetList)
                        if (clrTypeEdmSet2.EdmEntitySet == navigationBinding.Target && clrTypeEdmSet.EdmEntitySet != navigationBinding.Target)
                            return false;
                }
                else
                {
                    if (clrTypeEdmSet.EdmEntitySet == navigationBinding.Target)
                    {
                        IEdmStructuralProperty edmSelfRefProperty = navigationBinding.NavigationProperty.DependentProperties().Single();
                        selfRefProperty = clrTypeEdmSet.ClrTableType.GetProperty(edmSelfRefProperty.Name);
                    }
                }

            }
            return true;
        }
        public int SaveChanges(DataConnection dataConnection)
        {
            int count = 0;

            for (int i = _orderedTableTypes.Length - 1; i >= 0; i--)
            {
                OeLinq2DbTable table = GetTable(_orderedTableTypes[i].ClrTableType);
                if (table != null)
                {
                    count += table.SaveInserted(dataConnection);
                    UpdateIdentities(table, i);

                    count += table.SaveUpdated(dataConnection);
                }
            }

            for (int i = 0; i < _orderedTableTypes.Length; i++)
            {
                OeLinq2DbTable table = GetTable(_orderedTableTypes[i].ClrTableType);
                if (table != null)
                    count += table.SaveDeleted(dataConnection);
            }

            return count;
        }
        private void UpdateIdentities(OeLinq2DbTable table, int lastIndex)
        {
            if (table.Identities.Count == 0)
                return;

            foreach (IEdmNavigationPropertyBinding navigationBinding in _orderedTableTypes[lastIndex].EdmEntitySet.NavigationPropertyBindings)
                if (navigationBinding.NavigationProperty.IsPrincipal() || navigationBinding.NavigationProperty.Partner == null)
                    for (int j = 0; j <= lastIndex; j++)
                        if (_orderedTableTypes[j].EdmEntitySet == navigationBinding.Target)
                        {
                            List<PropertyInfo> dependentProperties = GetDependentProperties(_orderedTableTypes[j].ClrTableType, navigationBinding.NavigationProperty);
                            OeLinq2DbTable targetTable = GetTable(_orderedTableTypes[j].ClrTableType);
                            targetTable.UpdateIdentities(dependentProperties[0], table.Identities);

                            if (targetTable.IsKey(dependentProperties[0]))
                                foreach (PropertyInfo dependentProperty in GetDependentProperties(dependentProperties[0], _orderedTableTypes, j))
                                    GetTable(dependentProperty.DeclaringType).UpdateIdentities(dependentProperty, table.Identities);

                            break;
                        }
        }
    }
}
