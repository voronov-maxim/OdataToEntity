using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
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

        protected override OeShadowPropertyInfo CreateShadowProperty(INavigation efNavigation)
        {
            Type propertyType = efNavigation.IsDependentToPrincipal() ?
                efNavigation.ForeignKey.PrincipalEntityType.ClrType : efNavigation.ForeignKey.DeclaringEntityType.ClrType;

            if (efNavigation.IsCollection())
                propertyType = typeof(ICollection<>).MakeGenericType(new Type[] { propertyType });

            return new OeShadowPropertyInfo(efNavigation.DeclaringType.ClrType, propertyType, efNavigation.Name);
        }
        protected override OeShadowPropertyInfo CreateShadowProperty(IProperty efProperty)
        {
            DynamicTypeDefinition typeDefinition = _typeDefinitionManager.GetDynamicTypeDefinition(efProperty.DeclaringType.ClrType);
            MethodInfo getMethodInfo = typeDefinition.GetShadowPropertyGetMethodInfo(efProperty.Name, efProperty.ClrType);
            return new OeShadowPropertyInfo(efProperty.DeclaringType.ClrType, efProperty.ClrType, efProperty.Name, getMethodInfo);
        }
    }
}
