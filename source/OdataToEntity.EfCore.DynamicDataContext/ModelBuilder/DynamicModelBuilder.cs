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
        private readonly Dictionary<TableFullName, EntityType> _entityTypes;

        public DynamicModelBuilder(DynamicMetadataProvider metadataProvider, DynamicTypeDefinitionManager typeDefinitionManager)
        {
            MetadataProvider = metadataProvider;
            TypeDefinitionManager = typeDefinitionManager;

            _entityTypes = new Dictionary<TableFullName, EntityType>();
        }

        public void Build(Microsoft.EntityFrameworkCore.ModelBuilder modelBuilder)
        {
            foreach (TableFullName tableFullName in MetadataProvider.GetTableFullNames())
            {
                CreateEntityType(modelBuilder, tableFullName);
                CreateNavigationProperties(modelBuilder, tableFullName);
            }
        }
        private EntityType CreateEntityType(Microsoft.EntityFrameworkCore.ModelBuilder modelBuilder, in TableFullName tableFullName)
        {
            if (!_entityTypes.TryGetValue(tableFullName, out EntityType? entityType))
            {
                (String[] propertyNames, bool isPrimary)[] keys = MetadataProvider.GetKeys(tableFullName);
                bool isQueryType = keys.Length == 0 ? true : MetadataProvider.IsQueryType(tableFullName);

                String tableEdmName = MetadataProvider.GetTableEdmName(tableFullName);
                var dynamicTypeDefinition = TypeDefinitionManager.GetOrAddDynamicTypeDefinition(tableFullName, isQueryType, tableEdmName);
                EntityTypeBuilder entityTypeBuilder = modelBuilder.Entity(dynamicTypeDefinition.DynamicTypeType).ToTable(tableFullName.Name, tableFullName.Schema);

                entityType = (EntityType)entityTypeBuilder.Metadata;
                entityType.IsKeyless = isQueryType;
                foreach (DynamicPropertyInfo property in MetadataProvider.GetStructuralProperties(tableFullName))
                {
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

                _entityTypes.Add(tableFullName, entityType);
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
        private void CreateNavigationProperties(Microsoft.EntityFrameworkCore.ModelBuilder modelBuilder, in TableFullName tableFullName)
        {
            foreach (String propertyName in MetadataProvider.GetNavigationProperties(tableFullName))
            {
                DynamicDependentPropertyInfo dependentInfo = MetadataProvider.GetDependentProperties(tableFullName, propertyName);

                EntityType dependentEntityType = CreateEntityType(modelBuilder, dependentInfo.DependentTableName);
                EntityType principalEntityType = CreateEntityType(modelBuilder, dependentInfo.PrincipalTableName);

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

                DynamicTypeDefinition dynamicTypeDefinition = TypeDefinitionManager.GetDynamicTypeDefinition(tableFullName);
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
