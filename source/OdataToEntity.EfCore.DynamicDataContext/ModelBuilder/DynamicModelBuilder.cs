using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace OdataToEntity.EfCore.DynamicDataContext.ModelBuilder
{
    public class DynamicModelBuilder
    {
        private readonly Dictionary<String, EntityType> _entityTypes;

        public DynamicModelBuilder(DynamicMetadataProvider metadataProvider, DynamicTypeDefinitionManager typeDefinitionManager)
        {
            MetadataProvider = metadataProvider;
            TypeDefinitionManager = typeDefinitionManager;

            _entityTypes = new Dictionary<String, EntityType>();
        }

        public void Build(Microsoft.EntityFrameworkCore.ModelBuilder modelBuilder)
        {
            foreach ((String tableEdmName, bool isQueryType) in MetadataProvider.GetTableEdmNames())
            {
                CreateEntityType(modelBuilder, tableEdmName, isQueryType);
                CreateNavigationProperties(modelBuilder, tableEdmName);
            }
        }
        private EntityType CreateEntityType(Microsoft.EntityFrameworkCore.ModelBuilder modelBuilder, String tableEdmName, bool isQueryType)
        {
            if (!_entityTypes.TryGetValue(tableEdmName, out EntityType entityType))
            {
                var dynamicTypeDefinition = TypeDefinitionManager.GetOrAddDynamicTypeDefinition(tableEdmName, isQueryType);
                String tableSchema = MetadataProvider.GetTableSchema(tableEdmName);
                EntityTypeBuilder entityTypeBuilder = modelBuilder.Entity(dynamicTypeDefinition.DynamicTypeType).ToTable(tableEdmName, tableSchema);

                entityType = (EntityType)entityTypeBuilder.Metadata;
                foreach (DynamicPropertyInfo property in MetadataProvider.GetStructuralProperties(tableEdmName))
                {
                    String fieldName = dynamicTypeDefinition.AddShadowPropertyFieldInfo(property.Name, property.Type).Name;
                    PropertyBuilder propertyBuilder = entityTypeBuilder.Property(property.Type, property.Name).HasField(fieldName);
                    if (property.DatabaseGeneratedOption == DatabaseGeneratedOption.Identity)
                        propertyBuilder.ValueGeneratedOnAdd();
                    else if (property.DatabaseGeneratedOption == DatabaseGeneratedOption.Computed)
                        propertyBuilder.ValueGeneratedOnAddOrUpdate();
                    else
                        propertyBuilder.ValueGeneratedNever();
                }

                if (isQueryType)
                    entityTypeBuilder.Metadata.IsQueryType = true;
                else
                    entityTypeBuilder.HasKey(MetadataProvider.GetPrimaryKey(tableEdmName).ToArray());

                _entityTypes.Add(tableEdmName, entityType);
            }

            return entityType;
        }
        private void CreateNavigationProperties(Microsoft.EntityFrameworkCore.ModelBuilder modelBuilder, String tableName)
        {
            foreach (String propertyName in MetadataProvider.GetNavigationProperties(tableName))
            {
                DynamicDependentPropertyInfo dependentInfo = MetadataProvider.GetDependentProperties(tableName, propertyName);

                EntityType dependentEntityType = CreateEntityType(modelBuilder, MetadataProvider.GetTableName(dependentInfo.DependentEntityName), false);
                EntityType principalEntityType = CreateEntityType(modelBuilder, MetadataProvider.GetTableName(dependentInfo.PrincipalEntityName), false);

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

        public DynamicMetadataProvider MetadataProvider { get; }
        public DynamicTypeDefinitionManager TypeDefinitionManager { get; }
    }
}
