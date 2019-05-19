using Microsoft.OData.Edm;
using System;
using System.Collections.Generic;

namespace OdataToEntity.EfCore.DynamicDataContext
{
    public readonly struct DynamicMetadataProvider
    {
        public readonly struct DependentInfo
        {
            public DependentInfo(String principalEntityName, String dependentEntityName,
                IReadOnlyList<String> principalPropertyNames, IReadOnlyList<String> dependentPropertyNames, bool isCollection)
            {
                PrincipalEntityName = principalEntityName;
                DependentEntityName = dependentEntityName;
                PrincipalPropertyNames = principalPropertyNames;
                DependentPropertyNames = dependentPropertyNames;
                IsCollection = isCollection;
            }

            public String DependentEntityName { get; }
            public IReadOnlyList<String> DependentPropertyNames { get; }
            public bool IsCollection { get; }
            public String PrincipalEntityName { get; }
            public IReadOnlyList<String> PrincipalPropertyNames { get; }
        }

        private readonly IEdmModel _edmModel;

        public DynamicMetadataProvider(IEdmModel edmModel)
        {
            _edmModel = edmModel;
        }

        public DependentInfo GetDependentProperties(String tableName, String navigationPropertyName)
        {
            IEdmEntityType edmEntityType = OeEdmClrHelper.GetEntitySet(_edmModel, tableName).EntityType();
            var navigationProperty = (IEdmNavigationProperty)edmEntityType.GetPropertyIgnoreCase(navigationPropertyName);
            bool isCollection = navigationProperty.Type.IsCollection();

            String principalEntityName;
            String dependentEntityName;
            if (isCollection)
            {
                principalEntityName = edmEntityType.Name;
                dependentEntityName = navigationProperty.ToEntityType().Name;
            }
            else
            {
                principalEntityName = navigationProperty.ToEntityType().Name;
                dependentEntityName = edmEntityType.Name;
            }

            IEnumerable<IEdmStructuralProperty> principalProperties;
            IEnumerable<IEdmStructuralProperty> dependentProperties;
            if (navigationProperty.IsPrincipal())
            {
                principalProperties = navigationProperty.Partner.PrincipalProperties();
                dependentProperties = navigationProperty.Partner.DependentProperties();
            }
            else
            {

                principalProperties = navigationProperty.PrincipalProperties();
                dependentProperties = navigationProperty.DependentProperties();
            }

            var dependentPropertyNames = new List<String>();
            foreach (IEdmStructuralProperty structuralProperty in dependentProperties)
                dependentPropertyNames.Add(structuralProperty.Name);

            var principalPropertyNames = new List<String>();
            foreach (IEdmStructuralProperty structuralProperty in principalProperties)
                principalPropertyNames.Add(structuralProperty.Name);

            return new DependentInfo(principalEntityName, dependentEntityName,
                principalPropertyNames, dependentPropertyNames, isCollection);
        }

        public String GetEntityName(String tableName)
        {
            return OeEdmClrHelper.GetEntitySet(_edmModel, tableName).EntityType().Name;
        }
        public IEnumerable<(String, Type)> GetNavigationProperties(String tableName)
        {
            IEdmEntityType edmEntityType = OeEdmClrHelper.GetEntitySet(_edmModel, tableName).EntityType();
            Type clrType = _edmModel.GetClrType(edmEntityType);
            foreach (IEdmNavigationProperty navigationProperty in edmEntityType.NavigationProperties())
                if (!navigationProperty.ContainsTarget)
                    yield return (navigationProperty.Name, clrType.GetProperty(navigationProperty.Name).PropertyType);
        }
        public IEnumerable<String> GetPrimaryKey(String tableName)
        {
            IEdmEntityType edmEntityType = OeEdmClrHelper.GetEntitySet(_edmModel, tableName).EntityType();
            foreach (IEdmStructuralProperty structuralProperty in edmEntityType.Key())
                yield return structuralProperty.Name;
        }
        public IEnumerable<(String, Type)> GetStructuralProperties(String tableName)
        {
            IEdmEntityType edmEntityType = OeEdmClrHelper.GetEntitySet(_edmModel, tableName).EntityType();
            Type clrType = _edmModel.GetClrType(edmEntityType);
            foreach (IEdmStructuralProperty structuralProperty in edmEntityType.StructuralProperties())
                yield return (structuralProperty.Name, clrType.GetProperty(structuralProperty.Name).PropertyType);
        }
        public String GetTableName(String entityName)
        {
            foreach (IEdmEntitySet entitySet in _edmModel.EntityContainer.EntitySets())
                if (entitySet.EntityType().Name == entityName)
                    return entitySet.Name;

            throw new InvalidOperationException("Table for entity name " + entityName + " not found");
        }
        public IEnumerable<String> GetTableNames()
        {
            foreach (IEdmEntitySet entitySet in _edmModel.EntityContainer.EntitySets())
                yield return entitySet.Name;
        }
    }
}
