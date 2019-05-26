using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.OData.Edm;
using OdataToEntity.Infrastructure;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace OdataToEntity.EfCore.DynamicDataContext
{
    public sealed class DynamicEdmModelMetadataProvider : OeEfCoreEdmModelMetadataProvider
    {
        private readonly DynamicTypeDefinitionManager _typeDefinitionManager;

        public DynamicEdmModelMetadataProvider(IModel model, DynamicTypeDefinitionManager typeDefinitionManager) : base(model)
        {
            _typeDefinitionManager = typeDefinitionManager;
        }

        protected override OeShadowPropertyInfo CreateShadowProperty(IPropertyBase efProperty)
        {
            if (efProperty is INavigation efNavigation)
            {
                Type propertyType = efNavigation.IsDependentToPrincipal() ?
                    efNavigation.ForeignKey.PrincipalEntityType.ClrType : efNavigation.ForeignKey.DeclaringEntityType.ClrType;

                if (efNavigation.IsCollection())
                    propertyType = typeof(ICollection<>).MakeGenericType(new Type[] { propertyType });

                return new OeShadowPropertyInfo(efNavigation.DeclaringType.ClrType, propertyType, efNavigation.Name);
            }

            DynamicTypeDefinition typeDefinition = _typeDefinitionManager.GetDynamicTypeDefinition(efProperty.DeclaringType.ClrType);
            MethodInfo getMethodInfo = typeDefinition.AddShadowPropertyGetMethodInfo(efProperty.Name, efProperty.ClrType);
            return new OeShadowPropertyInfo(efProperty.DeclaringType.ClrType, efProperty.ClrType, efProperty.Name, getMethodInfo);
        }
        public override IReadOnlyList<PropertyInfo> GetManyToManyProperties(Type clrType)
        {
            if (clrType == typeof(DynamicType))
                return Array.Empty<PropertyInfo>();

            List<PropertyInfo> properties = null;
            String tableName = _typeDefinitionManager.GetDynamicTypeDefinition(clrType).TableName;
            foreach (var (propertyName, targetTableName) in _typeDefinitionManager.MetadataProvider.GetManyToManyProperties(tableName))
            {
                if (properties == null)
                    properties = new List<PropertyInfo>();

                Type itemType = _typeDefinitionManager.GetDynamicTypeDefinition(targetTableName).DynamicTypeType;
                Type propertyType = typeof(ICollection<>).MakeGenericType(itemType);
                properties.Add(new OeShadowPropertyInfo(clrType, propertyType, propertyName));
            }
            return properties ?? (IReadOnlyList<PropertyInfo>)Array.Empty<PropertyInfo>();
        }
    }
}
