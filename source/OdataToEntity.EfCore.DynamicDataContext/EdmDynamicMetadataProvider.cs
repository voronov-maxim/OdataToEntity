using Microsoft.EntityFrameworkCore;
using Microsoft.OData.Edm;
using OdataToEntity.ModelBuilder;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;

namespace OdataToEntity.EfCore.DynamicDataContext
{
    public sealed class EdmDynamicMetadataProvider : DynamicMetadataProvider
    {
        private readonly IEdmModel _edmModel;

        public EdmDynamicMetadataProvider(IEdmModel edmModel)
        {
            _edmModel = edmModel;
        }

        public override DynamicDependentPropertyInfo GetDependentProperties(String tableName, String navigationPropertyName)
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

            return new DynamicDependentPropertyInfo(principalEntityName, dependentEntityName,
                principalPropertyNames, dependentPropertyNames, isCollection);
        }
        public override String GetEntityName(String tableName)
        {
            return OeEdmClrHelper.GetEntitySet(_edmModel, tableName).EntityType().Name;
        }
        public override IEnumerable<(String NavigationName, String ManyToManyTarget)> GetManyToManyProperties(String tableName)
        {
            IEdmEntityType edmEntityType = OeEdmClrHelper.GetEntitySet(_edmModel, tableName).EntityType();
            foreach (IEdmNavigationProperty navigationProperty in edmEntityType.NavigationProperties())
                if (navigationProperty.ContainsTarget)
                {
                    ManyToManyJoinDescription joinDescription = _edmModel.GetManyToManyJoinDescription(navigationProperty);
                    IEdmEntitySet targetEntitySet = OeEdmClrHelper.GetEntitySet(_edmModel, joinDescription.TargetNavigationProperty);
                    yield return (navigationProperty.Name, targetEntitySet.Name);
                }
        }
        public override IEnumerable<String> GetNavigationProperties(String tableName)
        {
            IEdmEntityType edmEntityType = OeEdmClrHelper.GetEntitySet(_edmModel, tableName).EntityType();
            foreach (IEdmNavigationProperty navigationProperty in edmEntityType.NavigationProperties())
                if (!navigationProperty.ContainsTarget)
                    yield return navigationProperty.Name;
        }
        public override IEnumerable<String> GetPrimaryKey(String tableName)
        {
            IEdmEntityType edmEntityType = OeEdmClrHelper.GetEntitySet(_edmModel, tableName).EntityType();
            foreach (IEdmStructuralProperty structuralProperty in edmEntityType.Key())
                yield return structuralProperty.Name;
        }
        public override IEnumerable<DynamicPropertyInfo> GetStructuralProperties(String tableName)
        {
            IEdmEntityType edmEntityType = OeEdmClrHelper.GetEntitySet(_edmModel, tableName).EntityType();
            Type clrType = _edmModel.GetClrType(edmEntityType);
            foreach (IEdmStructuralProperty structuralProperty in edmEntityType.StructuralProperties())
            {
                PropertyInfo property = clrType.GetProperty(structuralProperty.Name);
                DatabaseGeneratedAttribute attribute = property.GetCustomAttribute<DatabaseGeneratedAttribute>();
                DatabaseGeneratedOption databaseGeneratedOption = attribute == null ?
                    DatabaseGeneratedOption.None : databaseGeneratedOption = attribute.DatabaseGeneratedOption;
                yield return new DynamicPropertyInfo(structuralProperty.Name, property.PropertyType, databaseGeneratedOption);
            }
        }
        public override String GetTableName(String entityName)
        {
            foreach (IEdmEntitySet entitySet in _edmModel.EntityContainer.EntitySets())
                if (entitySet.EntityType().Name == entityName)
                    return entitySet.Name;

            throw new InvalidOperationException("Table for entity name " + entityName + " not found");
        }
        public override IEnumerable<(String tableEdmName, bool isQueryType)> GetTableNames()
        {
            foreach (IEdmEntitySet entitySet in _edmModel.EntityContainer.EntitySets())
                yield return (entitySet.Name, false);
        }

        public override DbContextOptions DbContextOptions => throw new NotImplementedException();
    }
}
