using Microsoft.EntityFrameworkCore.Metadata;
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

        public override IReadOnlyList<PropertyInfo> GetManyToManyProperties(Type clrType)
        {
            if (clrType == typeof(Types.DynamicType))
                return Array.Empty<PropertyInfo>();

            List<PropertyInfo>? properties = null;
            String tableEdmName = _typeDefinitionManager.GetDynamicTypeDefinition(clrType).TableEdmName;
            foreach ((String navigationName, String manyToManyTarget) in _dynamicMetadataProvider.GetManyToManyProperties(tableEdmName))
            {
                if (properties == null)
                    properties = new List<PropertyInfo>();

                Type itemType = _typeDefinitionManager.GetDynamicTypeDefinition(manyToManyTarget).DynamicTypeType;
                Type propertyType = typeof(IEnumerable<>).MakeGenericType(itemType);
                properties.Add(new OeShadowPropertyInfo(clrType, propertyType, navigationName));
            }
            return properties ?? (IReadOnlyList<PropertyInfo>)Array.Empty<PropertyInfo>();
        }
        protected override PropertyInfo GetPropertyInfo(IPropertyBase efProperty)
        {
            Type propertyClrType;
            if (efProperty is INavigation navigation)
                propertyClrType = _typeDefinitionManager.GetDynamicTypeDefinition(navigation.DeclaringEntityType.ClrType).GetNavigationPropertyClrType(navigation);
            else
                propertyClrType = efProperty.ClrType;
            return base.CreateShadowProperty(efProperty, propertyClrType);
        }
    }
}
