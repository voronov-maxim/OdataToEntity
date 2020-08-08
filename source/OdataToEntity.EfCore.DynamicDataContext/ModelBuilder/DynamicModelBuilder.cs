using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;

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
                    DynamicTypeDefinition typeDefinition = TypeDefinitionManager.GetDynamicTypeDefinition(entityType.ClrType);
                    MethodInfo getMethodInfo = typeDefinition.AddShadowPropertyGetMethodInfo(property.Name, property.Type);
                    var propertyInfo = new Infrastructure.OeShadowPropertyInfo(entityType.ClrType, property.Type, property.Name, getMethodInfo);

                    var efProperty = (Property)entityType.AddProperty(propertyInfo);
                    efProperty.FieldInfo = dynamicTypeDefinition.AddShadowPropertyFieldInfo(property.Name, property.Type);
                    efProperty.SetIsNullable(property.IsNullable, ConfigurationSource.Explicit);
                    efProperty.SetPropertyAccessMode(PropertyAccessMode.Field);

                    if (property.DatabaseGeneratedOption == DatabaseGeneratedOption.Identity)
                        efProperty.SetValueGenerated(ValueGenerated.OnAdd, ConfigurationSource.Explicit);
                    else if (property.DatabaseGeneratedOption == DatabaseGeneratedOption.Computed)
                        efProperty.SetBeforeSaveBehavior(PropertySaveBehavior.Ignore);
                    else
                        efProperty.SetValueGenerated(ValueGenerated.Never, ConfigurationSource.Explicit);
                }

                if (isQueryType)
                    entityTypeBuilder.Metadata.IsKeyless = true;
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
                        pkey = principalEntityType.AddKey(principalProperties, ConfigurationSource.Explicit);

                    fkey = dependentEntityType.FindForeignKey(dependentProperties, pkey, principalEntityType);
                    if (fkey == null)
                        fkey = dependentEntityType.AddForeignKey(dependentProperties, pkey, principalEntityType, ConfigurationSource.Explicit, ConfigurationSource.Explicit);
                }

                DynamicTypeDefinition dynamicTypeDefinition = TypeDefinitionManager.GetDynamicTypeDefinition(tableEdmName);
                if (dependentInfo.IsCollection)
                {
                    if (!dependentEntityType.IsKeyless)
                    {
                        Type propertyType = typeof(IEnumerable<>).MakeGenericType(dependentEntityType.ClrType);
                        var shadowProperty = new Infrastructure.OeShadowPropertyInfo(principalEntityType.ClrType, propertyType, propertyName);
                        Navigation navigation = fkey.HasPrincipalToDependent(shadowProperty, ConfigurationSource.Explicit);
                        navigation.SetField(dynamicTypeDefinition.GetCollectionFiledName(propertyName));
                    }
                }
                else
                {
                    var shadowProperty = new Infrastructure.OeShadowPropertyInfo(dependentEntityType.ClrType, principalEntityType.ClrType, propertyName);
                    Navigation navigation = fkey.HasDependentToPrincipal(shadowProperty, ConfigurationSource.Explicit);
                    navigation.SetField(dynamicTypeDefinition.GetSingleFieldName(propertyName));
                }
            }
        }

        public DynamicMetadataProvider MetadataProvider { get; }
        public DynamicTypeDefinitionManager TypeDefinitionManager { get; }
    }
}
