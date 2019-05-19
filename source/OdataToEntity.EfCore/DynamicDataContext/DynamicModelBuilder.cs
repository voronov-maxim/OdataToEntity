using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OdataToEntity.EfCore.DynamicDataContext
{
    public class DynamicModelBuilder
    {
        private readonly Dictionary<String, EntityType> _entityTypes;

        public DynamicModelBuilder(DynamicTypeDefinitionManager typeDefinitionManager)
        {
            TypeDefinitionManager = typeDefinitionManager;

            _entityTypes = new Dictionary<String, EntityType>();
        }

        public void Build(Microsoft.EntityFrameworkCore.ModelBuilder modelBuilder)
        {
            foreach (String tableName in MetadataProvider.GetTableNames())
            {
                CreateEntityType(modelBuilder, tableName);
                CreateNavigationProperties(modelBuilder, tableName);
            }
        }
        private EntityType CreateEntityType(Microsoft.EntityFrameworkCore.ModelBuilder modelBuilder, String tableName)
        {
            if (!_entityTypes.TryGetValue(tableName, out EntityType entityType))
            {
                var dynamicTypeDefinition = TypeDefinitionManager.GetDynamicTypeDefinition(tableName);
                EntityTypeBuilder entityTypeBuilder = modelBuilder.Entity(dynamicTypeDefinition.DynamicTypeType).ToTable(tableName);

                foreach (var (propertyName, propertyType) in MetadataProvider.GetStructuralProperties(tableName))
                    entityTypeBuilder.Property(propertyType, propertyName);

                entityTypeBuilder.HasKey(MetadataProvider.GetPrimaryKey(tableName).ToArray());

                entityType = (EntityType)entityTypeBuilder.Metadata;
                _entityTypes.Add(tableName, entityType);
            }

            return entityType;
        }
        private void CreateNavigationProperties(Microsoft.EntityFrameworkCore.ModelBuilder modelBuilder, String tableName)
        {
            foreach (var (propertyName, propertyType) in MetadataProvider.GetNavigationProperties(tableName))
            {
                DynamicMetadataProvider.DependentInfo dependentInfo = MetadataProvider.GetDependentProperties(tableName, propertyName);

                EntityType dependentEntityType = CreateEntityType(modelBuilder, MetadataProvider.GetTableName(dependentInfo.DependentEntityName));
                EntityType principalEntityType = CreateEntityType(modelBuilder, MetadataProvider.GetTableName(dependentInfo.PrincipalEntityName));

                var dependentProperties = new List<Property>();
                foreach (String dependentPropertyName in dependentInfo.DependentPropertyNames)
                    dependentProperties.Add((Property)dependentEntityType.GetProperty(dependentPropertyName));

                ForeignKey fkey = dependentEntityType.FindForeignKey(dependentProperties, principalEntityType.FindPrimaryKey(), principalEntityType);
                if (fkey == null)
                {
                    var principalProperties = new List<Property>();
                    foreach (String principalPropertyName in dependentInfo.PrincipalPropertyNames)
                        principalProperties.Add((Property)principalEntityType.GetProperty(principalPropertyName));

                    Key pkey = principalEntityType.FindKey(principalProperties);
                    if (pkey == null)
                        pkey = principalEntityType.AddKey(principalProperties);

                    fkey = dependentEntityType.AddForeignKey(dependentProperties, pkey, principalEntityType);
                }

                DynamicTypeDefinition dynamicTypeDefinition = TypeDefinitionManager.GetDynamicTypeDefinition(tableName);
                if (dependentInfo.IsCollection)
                {
                    Navigation navigation = fkey.HasPrincipalToDependent(propertyName);
                    navigation.SetField(dynamicTypeDefinition.GetCollectionFiledName(propertyName));
                }
                else
                {
                    Navigation navigation = fkey.HasDependentToPrincipal(propertyName);
                    navigation.SetField(dynamicTypeDefinition.GetSingleFiledName(propertyName));
                }
            }
        }

        public DynamicTypeDefinitionManager TypeDefinitionManager { get; }
        public DynamicMetadataProvider MetadataProvider => TypeDefinitionManager.MetadataProvider;
    }
}
