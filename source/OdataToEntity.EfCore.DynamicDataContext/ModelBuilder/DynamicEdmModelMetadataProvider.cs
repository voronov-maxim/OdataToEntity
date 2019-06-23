using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.OData.Edm;
using OdataToEntity.Infrastructure;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace OdataToEntity.EfCore.DynamicDataContext.ModelBuilder
{
    public sealed class DynamicEdmModelMetadataProvider : OeEfCoreEdmModelMetadataProvider
    {
        private readonly DynamicTypeDefinitionManager _typeDefinitionManager;
        private readonly DynamicMetadataProvider _dynamicMetadataProvider;

        public DynamicEdmModelMetadataProvider(IModel model, DynamicMetadataProvider dynamicMetadataProvider, DynamicTypeDefinitionManager typeDefinitionManager) : base(model)
        {
            _dynamicMetadataProvider = dynamicMetadataProvider;
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
            if (clrType == typeof(Types.DynamicType))
                return Array.Empty<PropertyInfo>();

            List<PropertyInfo> properties = null;
            String tableEdmName = _typeDefinitionManager.GetDynamicTypeDefinition(clrType).TableEdmName;
            foreach ((String navigationName, String manyToManyTarget) in _dynamicMetadataProvider.GetManyToManyProperties(tableEdmName))
            {
                if (properties == null)
                    properties = new List<PropertyInfo>();

                Type itemType = _typeDefinitionManager.GetDynamicTypeDefinition(manyToManyTarget).DynamicTypeType;
                Type propertyType = typeof(ICollection<>).MakeGenericType(itemType);
                properties.Add(new OeShadowPropertyInfo(clrType, propertyType, navigationName));
            }
            return properties ?? (IReadOnlyList<PropertyInfo>)Array.Empty<PropertyInfo>();
        }
    }
}
