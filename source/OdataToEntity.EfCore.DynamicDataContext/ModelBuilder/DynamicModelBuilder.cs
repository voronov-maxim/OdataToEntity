using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
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
            if (!_entityTypes.TryGetValue(tableEdmName, out EntityType? entityType))
            {
                (String[] propertyNames, bool isPrimary)[] keys = MetadataProvider.GetKeys(tableEdmName);
                if (keys.Length == 0)
                    isQueryType = true;

                var dynamicTypeDefinition = TypeDefinitionManager.GetOrAddDynamicTypeDefinition(tableEdmName, isQueryType);
                String tableSchema = MetadataProvider.GetTableSchema(tableEdmName);
                String tableDbName = tableEdmName;
                if (tableDbName.StartsWith(tableSchema + "."))
                    tableDbName = tableDbName.Substring(tableSchema.Length + 1);
                EntityTypeBuilder entityTypeBuilder = modelBuilder.Entity(dynamicTypeDefinition.DynamicTypeType).ToTable(tableDbName, tableSchema);

                entityType = (EntityType)entityTypeBuilder.Metadata;
                entityType.IsKeyless = isQueryType;
                foreach (DynamicPropertyInfo property in MetadataProvider.GetStructuralProperties(tableEdmName))
                {
                    DynamicTypeDefinition typeDefinition = TypeDefinitionManager.GetDynamicTypeDefinition(entityType.ClrType);

                    var efProperty = (Property)entityType.AddIndexerProperty(property.Name, property.Type);
                    efProperty.SetIsNullable(property.IsNullable, ConfigurationSource.Explicit);

                    if (property.DatabaseGeneratedOption == DatabaseGeneratedOption.Identity)
                    {
                        FixHasDefaultValue(efProperty);
                        efProperty.SetValueGenerated(ValueGenerated.OnAdd, ConfigurationSource.Explicit);
                    }
                    else if (property.DatabaseGeneratedOption == DatabaseGeneratedOption.Computed)
                        efProperty.SetBeforeSaveBehavior(PropertySaveBehavior.Ignore);
                    else
                        efProperty.SetValueGenerated(ValueGenerated.Never, ConfigurationSource.Explicit);
                }

                if (!isQueryType)
                    foreach ((String[] propertyNames, bool isPrimary) in keys)
                        if (isPrimary)
                            entityTypeBuilder.HasKey(propertyNames);
                        else
                            entityTypeBuilder.HasAlternateKey(propertyNames);

                _entityTypes.Add(tableEdmName, entityType);
            }

            return entityType;

            //fix ef core 5.0 bug get indexer properties
            static void FixHasDefaultValue(IProperty efProperty)
            {
                IClrPropertyGetter propertyGetter = efProperty.GetGetter();
                FieldInfo hasDefaultValue = propertyGetter.GetType().GetField("_hasDefaultValue", BindingFlags.Instance | BindingFlags.NonPublic)!;
                hasDefaultValue.SetValue(propertyGetter, GetDefaultValueFunc(efProperty));
            }
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
                        Navigation navigation = fkey.HasPrincipalToDependent(propertyName, ConfigurationSource.Explicit);
                        dynamicTypeDefinition.AddNavigationProperty(navigation, propertyType);
                    }
                }
                else
                {
                    Navigation navigation = fkey.SetDependentToPrincipal(propertyName, ConfigurationSource.Explicit);
                    dynamicTypeDefinition.AddNavigationProperty(navigation, principalEntityType.ClrType);
                }
            }
        }
        private static Delegate GetDefaultValueFunc(IProperty property)
        {
            ParameterExpression parameterExpression = Expression.Parameter(property.DeclaringEntityType.ClrType);
            Expression expression = PropertyBase.CreateMemberAccess(property, parameterExpression, property.PropertyInfo);
            expression = Expression.Convert(expression, property.ClrType);
            expression = expression.MakeHasDefaultValue(property);
            return Expression.Lambda(expression, new[] { parameterExpression }).Compile();
        }

        public DynamicMetadataProvider MetadataProvider { get; }
        public DynamicTypeDefinitionManager TypeDefinitionManager { get; }
    }
}
