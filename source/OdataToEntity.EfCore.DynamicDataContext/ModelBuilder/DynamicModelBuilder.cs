using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

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
                (String[] propertyNames, bool isPrimary)[] keys = MetadataProvider.GetKeys(tableEdmName);
                if (keys.Length == 0)
                    isQueryType = true;

                var dynamicTypeDefinition = TypeDefinitionManager.GetOrAddDynamicTypeDefinition(tableEdmName, isQueryType);
                String tableSchema = MetadataProvider.GetTableSchema(tableEdmName);
                EntityTypeBuilder entityTypeBuilder = modelBuilder.Entity(dynamicTypeDefinition.DynamicTypeType).ToTable(tableEdmName, tableSchema);

                entityType = (EntityType)entityTypeBuilder.Metadata;
                foreach (DynamicPropertyInfo property in MetadataProvider.GetStructuralProperties(tableEdmName))
                {
                    String fieldName = dynamicTypeDefinition.AddShadowPropertyFieldInfo(property.Name, property.Type).Name;
                    PropertyBuilder propertyBuilder = entityTypeBuilder.Property(property.Type, property.Name).IsRequired(!property.IsNullable).HasField(fieldName);
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
                    foreach ((String[] propertyNames, bool isPrimary) in keys)
                        if (isPrimary)
                            entityTypeBuilder.HasKey(propertyNames);
                        else
                            entityTypeBuilder.HasAlternateKey(propertyNames);

                _entityTypes.Add(tableEdmName, entityType);
            }

            return entityType;
        }
        private void CreateNavigationProperties(Microsoft.EntityFrameworkCore.ModelBuilder modelBuilder, String tableEdmName)
        {
            foreach (String propertyName in MetadataProvider.GetNavigationProperties(tableEdmName))
            {
                DynamicDependentPropertyInfo dependentInfo = MetadataProvider.GetDependentProperties(tableEdmName, propertyName);

                EntityType dependentEntityType = CreateEntityType(modelBuilder, MetadataProvider.GetTableEdmName(dependentInfo.DependentEntityName), false);
                EntityType principalEntityType = CreateEntityType(modelBuilder, MetadataProvider.GetTableEdmName(dependentInfo.PrincipalEntityName), false);

                var dependentProperties = new Property[dependentInfo.DependentPropertyNames.Count];
                for (int i = 0; i < dependentProperties.Length; i++)
                    dependentProperties[i] = (Property)dependentEntityType.GetProperty(dependentInfo.DependentPropertyNames[i]);

                ForeignKey fkey = dependentEntityType.FindForeignKey(dependentProperties, principalEntityType.FindPrimaryKey(), principalEntityType);
                if (fkey == null)
                {
                    var principalProperties = new List<Property>();
                    foreach (String principalPropertyName in dependentInfo.PrincipalPropertyNames)
                        principalProperties.Add((Property)principalEntityType.GetProperty(principalPropertyName));

                    Key pkey = principalEntityType.FindKey(principalProperties);
                    if (pkey == null)
                        pkey = principalEntityType.AddKey(principalProperties);

                    fkey = dependentEntityType.FindForeignKey(dependentProperties, pkey, principalEntityType);
                    if (fkey == null)
                        fkey = dependentEntityType.AddForeignKey(dependentProperties, pkey, principalEntityType);
                }

                DynamicTypeDefinition dynamicTypeDefinition = TypeDefinitionManager.GetDynamicTypeDefinition(tableEdmName);
                if (dependentInfo.IsCollection)
                {
                    if (!dependentEntityType.IsQueryType)
                    {
                        Navigation navigation = fkey.HasPrincipalToDependent(propertyName);
                        navigation.SetField(dynamicTypeDefinition.GetCollectionFiledName(propertyName));
                    }
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
