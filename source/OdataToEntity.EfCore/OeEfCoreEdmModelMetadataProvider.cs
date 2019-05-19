using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
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

        public OeEfCoreEdmModelMetadataProvider(IModel model)
        {
            _efModel = model;
            _entityTypes = _efModel.GetEntityTypes().ToDictionary(e => e.ClrType);
        }

        protected virtual Infrastructure.OeShadowPropertyInfo CreateShadowProperty(INavigation efNavigation)
        {
            return new Infrastructure.OeShadowPropertyInfo(efNavigation.DeclaringType.ClrType, efNavigation.ClrType, efNavigation.Name);
        }
        protected virtual Infrastructure.OeShadowPropertyInfo CreateShadowProperty(IProperty efProperty)
        {
            return new Infrastructure.OeShadowPropertyInfo(efProperty.DeclaringType.ClrType, efProperty.ClrType, efProperty.Name);
        }
        private IEnumerable<IEntityType> GetEntityTypes(PropertyInfo propertyInfo)
        {
            if (_entityTypes.TryGetValue(propertyInfo.DeclaringType, out IEntityType efEntityType))
                yield return efEntityType;
            else
                foreach (KeyValuePair<Type, IEntityType> pair in _entityTypes)
                    if (propertyInfo.DeclaringType.IsAssignableFrom(pair.Key))
                        yield return pair.Value;
        }
        public override PropertyInfo[] GetForeignKey(PropertyInfo propertyInfo)
        {
            foreach (IEntityType efEntityType in GetEntityTypes(propertyInfo))
                foreach (IForeignKey fkey in efEntityType.GetForeignKeys())
                {
                    if (fkey.PrincipalEntityType == efEntityType && fkey.PrincipalEntityType != fkey.DeclaringEntityType)
                        continue;

                    if (fkey.DependentToPrincipal != null && fkey.DependentToPrincipal.Name == propertyInfo.Name)
                    {
                        var properties = new PropertyInfo[fkey.Properties.Count];
                        for (int i = 0; i < fkey.Properties.Count; i++)
                        {
                            IProperty efProperty = fkey.Properties[i];
                            properties[i] = efProperty.IsShadowProperty ?
                                CreateShadowProperty(efProperty) : propertyInfo.DeclaringType.GetPropertyIgnoreCase(efProperty.Name);
                        }
                        return properties;
                    }

                    for (int i = 0; i < fkey.Properties.Count; i++)
                        if (fkey.Properties[i].Name == propertyInfo.Name && fkey.DependentToPrincipal != null)
                            return new PropertyInfo[] { propertyInfo.DeclaringType.GetPropertyIgnoreCase(fkey.DependentToPrincipal.Name) };
                }

            return null;
        }
        public override PropertyInfo GetInverseProperty(PropertyInfo propertyInfo)
        {
            foreach (IEntityType efEntityType in GetEntityTypes(propertyInfo))
            {
                foreach (IForeignKey fkey in efEntityType.GetForeignKeys())
                    if (fkey.DependentToPrincipal != null && fkey.DependentToPrincipal.Name == propertyInfo.Name)
                    {
                        INavigation efInverseProperty = fkey.DependentToPrincipal.FindInverse();
                        if (efInverseProperty != null)
                        {
                            PropertyInfo clrInverseProperty = efInverseProperty.DeclaringEntityType.ClrType.GetPropertyIgnoreCase(efInverseProperty.Name);
                            if (clrInverseProperty == null)
                                clrInverseProperty = CreateShadowProperty(efInverseProperty);
                            return clrInverseProperty;
                        }
                    }

                foreach (IForeignKey fkey in efEntityType.GetReferencingForeignKeys())
                    if (fkey.PrincipalToDependent != null && fkey.PrincipalToDependent.Name == propertyInfo.Name)
                    {
                        INavigation efInverseProperty = fkey.PrincipalToDependent.FindInverse();
                        if (efInverseProperty != null)
                        {
                            PropertyInfo clrInverseProperty = efInverseProperty.DeclaringEntityType.ClrType.GetPropertyIgnoreCase(efInverseProperty.Name);
                            if (clrInverseProperty == null)
                                clrInverseProperty = CreateShadowProperty(efInverseProperty);
                            return clrInverseProperty;
                        }
                    }
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
        public override PropertyInfo[] GetPrincipalToDependentWithoutDependent(PropertyInfo propertyInfo)
        {
            foreach (IEntityType efEntityType in GetEntityTypes(propertyInfo))
                foreach (IForeignKey fkey in efEntityType.GetReferencingForeignKeys())
                    if (fkey.PrincipalToDependent != null &&
                        fkey.PrincipalToDependent.Name == propertyInfo.Name &&
                        fkey.PrincipalToDependent.IsCollection() &&
                        fkey.PrincipalToDependent.FindInverse() == null)
                    {
                        var properties = new PropertyInfo[fkey.Properties.Count];
                        for (int i = 0; i < fkey.Properties.Count; i++)
                        {
                            IProperty efProperty = fkey.Properties[i];
                            properties[i] = efProperty.IsShadowProperty ?
                                CreateShadowProperty(efProperty) : fkey.DeclaringEntityType.ClrType.GetPropertyIgnoreCase(efProperty.Name);
                        }
                        return properties;
                    }

            return null;
        }
        public override PropertyInfo[] GetProperties(Type type)
        {
            PropertyInfo[] clrProperties = base.GetProperties(type);

            if (_entityTypes.TryGetValue(type, out IEntityType efEntityType))
            {
                List<PropertyInfo> clrPropertyList = null;
                foreach (IProperty efProperty in efEntityType.GetProperties())
                    if (efProperty.IsShadowProperty)
                    {
                        if (clrPropertyList == null)
                            clrPropertyList = new List<PropertyInfo>(clrProperties);
                        clrPropertyList.Add(CreateShadowProperty(efProperty));
                    }

                foreach (INavigation navigation in efEntityType.GetNavigations())
                    if (Array.Find(clrProperties, p => p.Name == navigation.Name) == null)
                    {
                        if (clrPropertyList == null)
                            clrPropertyList = new List<PropertyInfo>(clrProperties);
                        clrPropertyList.Add(CreateShadowProperty(navigation));
                    }

                if (clrPropertyList != null)
                    return clrPropertyList.ToArray();
            }

            return clrProperties;
        }
        public override bool IsKey(PropertyInfo propertyInfo)
        {
            foreach (IEntityType efEntityType in GetEntityTypes(propertyInfo))
            {
                if (efEntityType.IsQueryType)
                    continue;

                IKey key = efEntityType.FindPrimaryKey();
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
