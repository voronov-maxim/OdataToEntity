using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using OdataToEntity.ModelBuilder;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace OdataToEntity.EfCore
{
    public class OeEfCoreEdmModelMetadataProvider : OeEdmModelMetadataProvider
    {
        private readonly IModel _efModel;
        private readonly Dictionary<Type, IEntityType> _entityTypes;
        private readonly Dictionary<IPropertyBase, Infrastructure.OeShadowPropertyInfo> _shadowProperties;

        public OeEfCoreEdmModelMetadataProvider(IModel model)
        {
            _efModel = model;
            _entityTypes = _efModel.GetEntityTypes().ToDictionary(e => e.ClrType);
            _shadowProperties = new Dictionary<IPropertyBase, Infrastructure.OeShadowPropertyInfo>();
        }

        protected Infrastructure.OeShadowPropertyInfo CreateShadowProperty(IPropertyBase efProperty, Type propertyClrType)
        {
            if (!_shadowProperties.TryGetValue(efProperty, out Infrastructure.OeShadowPropertyInfo? shadowProperty))
            {
                shadowProperty = new Infrastructure.OeShadowPropertyInfo(efProperty.DeclaringType.ClrType, propertyClrType, efProperty.Name);
                _shadowProperties.Add(efProperty, shadowProperty);
            }
            return shadowProperty;
        }
        private IEnumerable<IEntityType> GetEntityTypes(PropertyInfo propertyInfo)
        {
            if (propertyInfo.DeclaringType == null)
                throw new InvalidOperationException("PropertyInfo.DeclaringType is null");

            if (_entityTypes.TryGetValue(propertyInfo.DeclaringType, out IEntityType? efEntityType))
                yield return efEntityType;
            else
                foreach (KeyValuePair<Type, IEntityType> pair in _entityTypes)
                    if (propertyInfo.DeclaringType.IsAssignableFrom(pair.Key))
                        yield return pair.Value;
        }
        public override PropertyInfo[]? GetForeignKey(PropertyInfo propertyInfo)
        {
            if (propertyInfo.DeclaringType == null)
                throw new InvalidOperationException("PropertyInfo.DeclaringType is null");

            foreach (IEntityType efEntityType in GetEntityTypes(propertyInfo))
                foreach (IForeignKey fkey in efEntityType.GetForeignKeys())
                {
                    if (fkey.PrincipalEntityType == efEntityType && fkey.PrincipalEntityType != fkey.DeclaringEntityType)
                        continue;

                    if (fkey.DependentToPrincipal != null && fkey.DependentToPrincipal.Name == propertyInfo.Name)
                    {
                        var properties = new PropertyInfo[fkey.Properties.Count];
                        for (int i = 0; i < fkey.Properties.Count; i++)
                            properties[i] = GetPropertyInfo(fkey.Properties[i]);
                        return properties;
                    }
                }

            return null;
        }
        public override PropertyInfo? GetInverseProperty(PropertyInfo propertyInfo)
        {
            foreach (IEntityType efEntityType in GetEntityTypes(propertyInfo))
            {
                foreach (IForeignKey fkey in efEntityType.GetForeignKeys())
                    if (fkey.DependentToPrincipal != null &&
                        fkey.DependentToPrincipal.Name == propertyInfo.Name &&
                        fkey.DependentToPrincipal.Inverse != null)
                        return GetPropertyInfo(fkey.DependentToPrincipal.Inverse);

                foreach (IForeignKey fkey in efEntityType.GetReferencingForeignKeys())
                    if (fkey.PrincipalToDependent != null &&
                        fkey.PrincipalToDependent.Name == propertyInfo.Name &&
                        fkey.PrincipalToDependent.Inverse != null)
                        return GetPropertyInfo(fkey.PrincipalToDependent.Inverse);
            }

            return null;
        }
        public override int GetOrder(PropertyInfo propertyInfo)
        {
            foreach (IEntityType efEntityType in GetEntityTypes(propertyInfo))
            {
                IKey key = efEntityType.FindPrimaryKey();
                for (int i = 0; i < key.Properties.Count; i++)
                    if (key.Properties[i].Name == propertyInfo.Name)
                        return i;

                foreach (IForeignKey fkey in efEntityType.GetForeignKeys())
                    for (int i = 0; i < fkey.Properties.Count; i++)
                        if (fkey.Properties[i].Name == propertyInfo.Name)
                            return i;
            }

            return -1;
        }
        public override PropertyInfo[] GetPrincipalStructuralProperties(PropertyInfo principalNavigation)
        {
            foreach (EntityType efEntityType in GetEntityTypes(principalNavigation))
            {
                Navigation navigation = efEntityType.FindNavigation(principalNavigation);
                if (navigation != null)
                {
                    var propertyInfos = new PropertyInfo[navigation.ForeignKey.PrincipalKey.Properties.Count];
                    for (int i = 0; i < propertyInfos.Length; i++)
                        propertyInfos[i] = GetPropertyInfo(navigation.ForeignKey.PrincipalKey.Properties[i]);
                    return propertyInfos;
                }
            }

            throw new InvalidOperationException("Not found key for principal navigation property " + principalNavigation.DeclaringType?.Name + "." + principalNavigation.Name);
        }
        public override PropertyInfo[] GetPrincipalToDependentWithoutDependent(PropertyInfo principalNavigation)
        {
            foreach (IEntityType efEntityType in GetEntityTypes(principalNavigation))
                foreach (IForeignKey fkey in efEntityType.GetReferencingForeignKeys())
                    if (fkey.PrincipalToDependent != null &&
                        fkey.PrincipalToDependent.Name == principalNavigation.Name &&
                        fkey.PrincipalToDependent.IsCollection &&
                        fkey.PrincipalToDependent.Inverse == null)
                    {
                        var properties = new PropertyInfo[fkey.Properties.Count];
                        for (int i = 0; i < fkey.Properties.Count; i++)
                            properties[i] = GetPropertyInfo(fkey.Properties[i]);
                        return properties;
                    }

            throw new InvalidOperationException("Pricipal structurlal property not found for principal navigation " + principalNavigation.Name);
        }
        public override PropertyInfo[] GetProperties(Type type)
        {
            if (_entityTypes.TryGetValue(type, out IEntityType? efEntityType))
            {
                var properties = new List<PropertyInfo>();
                foreach (IProperty efProperty in efEntityType.GetDeclaredProperties())
                    if (efProperty.PropertyInfo == null || efProperty.PropertyInfo.DeclaringType == type || efProperty.IsIndexerProperty())
                        properties.Add(GetPropertyInfo(efProperty));
                foreach (INavigation navigation in efEntityType.GetDeclaredNavigations())
                    if (navigation.PropertyInfo == null || navigation.PropertyInfo.DeclaringType == type)
                        properties.Add(GetPropertyInfo(navigation));
                return properties.ToArray();
            }

            return base.GetProperties(type);
        }
        protected virtual PropertyInfo GetPropertyInfo(IPropertyBase efProperty)
        {
            if (efProperty.IsShadowProperty())
                return CreateShadowProperty(efProperty, efProperty.ClrType);

            if (efProperty.PropertyInfo == null)
                return efProperty.DeclaringType.ClrType.GetPropertyIgnoreCase(efProperty.Name);

            return efProperty.PropertyInfo;
        }
        public override bool IsDatabaseGenerated(PropertyInfo propertyInfo)
        {
            foreach (IEntityType efEntityType in GetEntityTypes(propertyInfo))
            {
                IProperty efProperty = efEntityType.FindProperty(propertyInfo.Name);
                if (efProperty != null)
                    return efProperty.ValueGenerated != ValueGenerated.Never ||
                        efProperty.GetBeforeSaveBehavior() == PropertySaveBehavior.Ignore;
            }

            throw new InvalidOperationException("Property " + propertyInfo.Name + " not found");
        }
        public override bool IsKey(PropertyInfo propertyInfo)
        {
            foreach (IEntityType efEntityType in GetEntityTypes(propertyInfo))
            {
                IKey key = efEntityType.FindPrimaryKey();
                if (key != null)
                    for (int i = 0; i < key.Properties.Count; i++)
                        if (key.Properties[i].Name == propertyInfo.Name)
                            return true;
            }

            return false;
        }
        public override bool IsNotMapped(PropertyInfo propertyInfo)
        {
            foreach (IEntityType efEntityType in GetEntityTypes(propertyInfo))
            {
                foreach (IProperty efProperty in efEntityType.GetProperties())
                    if (efProperty.Name == propertyInfo.Name)
                        return false;

                foreach (IForeignKey fkey in efEntityType.GetForeignKeys())
                    if (fkey.DependentToPrincipal != null && fkey.DependentToPrincipal.Name == propertyInfo.Name)
                        return false;

                foreach (IForeignKey fkey in efEntityType.GetReferencingForeignKeys())
                    if (fkey.PrincipalToDependent != null && fkey.PrincipalToDependent.Name == propertyInfo.Name)
                        return false;
            }

            return true;
        }
        public override bool IsRequired(PropertyInfo propertyInfo)
        {
            foreach (IEntityType efEntityType in GetEntityTypes(propertyInfo))
            {
                foreach (IProperty efProperty in efEntityType.GetProperties())
                    if (efProperty.Name == propertyInfo.Name)
                        return !efProperty.IsNullable;

                foreach (IForeignKey fkey in efEntityType.GetForeignKeys())
                    if (fkey.DependentToPrincipal != null && fkey.DependentToPrincipal.Name == propertyInfo.Name)
                        return fkey.IsRequired;

                foreach (IForeignKey fkey in efEntityType.GetReferencingForeignKeys())
                    if (fkey.PrincipalToDependent != null && fkey.PrincipalToDependent.Name == propertyInfo.Name)
                        return fkey.IsRequired;
            }

            throw new InvalidOperationException("property " + propertyInfo.Name + " not found");
        }
    }
}
