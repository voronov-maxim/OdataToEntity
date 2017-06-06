using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using OdataToEntity.ModelBuilder;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace OdataToEntity.EfCore
{
    public sealed class OeEfCoreEdmModelMetadataProvider : OeEdmModelMetadataProvider
    {
        private readonly IModel _efModel;
        private readonly Dictionary<Type, IEntityType> _entityTypes;

        public OeEfCoreEdmModelMetadataProvider(IModel model)
        {
            _efModel = model;
            _entityTypes = _efModel.GetEntityTypes().ToDictionary(e => e.ClrType);
        }

        public override PropertyDescriptor[] GetForeignKey(PropertyDescriptor propertyDescriptor)
        {
            IEntityType efEntityType = _entityTypes[propertyDescriptor.ComponentType];
            foreach (IForeignKey fkey in efEntityType.GetForeignKeys())
            {
                bool isPrincipal = fkey.PrincipalEntityType == efEntityType;
                IReadOnlyList<IProperty> structuralProperties = isPrincipal ? fkey.PrincipalKey.Properties : fkey.Properties;
                INavigation navigationProperty = isPrincipal ? fkey.PrincipalToDependent : fkey.DependentToPrincipal;

                if (navigationProperty.Name == propertyDescriptor.Name)
                    return fkey.PrincipalEntityType == fkey.DeclaringEntityType ? null : GetPropertyDescriptors(structuralProperties);

                for (int i = 0; i < structuralProperties.Count; i++)
                    if (structuralProperties[i].IsForeignKey() && structuralProperties[i].Name == propertyDescriptor.Name)
                        return new PropertyDescriptor[] { TypeDescriptor.GetProperties(propertyDescriptor.ComponentType)[navigationProperty.Name] };

                if (fkey.PrincipalEntityType == fkey.DeclaringEntityType)
                    if (fkey.DependentToPrincipal.Name == propertyDescriptor.Name)
                        return GetPropertyDescriptors(fkey.Properties);
            }

            return null;
        }
        public override PropertyDescriptor GetInverseProperty(PropertyDescriptor propertyDescriptor)
        {
            IEntityType efEntityType = _entityTypes[propertyDescriptor.ComponentType];
            foreach (IForeignKey fkey in efEntityType.GetForeignKeys())
            {
                INavigation navigationProperty = fkey.PrincipalEntityType == efEntityType ? fkey.PrincipalToDependent : fkey.DependentToPrincipal;
                if (navigationProperty.Name == propertyDescriptor.Name)
                {
                    INavigation inverseProperty = navigationProperty.FindInverse();
                    return inverseProperty == null ? null : TypeDescriptor.GetProperties(propertyDescriptor.ComponentType)[inverseProperty.Name];
                }

                if (fkey.PrincipalEntityType == fkey.DeclaringEntityType)
                    if (fkey.DependentToPrincipal.Name == propertyDescriptor.Name)
                    {
                        INavigation inverseProperty = fkey.DependentToPrincipal.FindInverse();
                        return inverseProperty == null ? null : TypeDescriptor.GetProperties(propertyDescriptor.ComponentType)[inverseProperty.Name];
                    }
            }

            foreach (IForeignKey fkey in efEntityType.GetReferencingForeignKeys())
                if (fkey.PrincipalToDependent.Name == propertyDescriptor.Name)
                {
                    INavigation inverseProperty = fkey.PrincipalToDependent.FindInverse();
                    if (inverseProperty == null)
                        break;

                    return TypeDescriptor.GetProperties(inverseProperty.DeclaringEntityType.ClrType)[inverseProperty.Name];
                }

            return null;
        }
        public override int GetOrder(PropertyDescriptor propertyDescriptor)
        {
            IEntityType efEntityType = _entityTypes[propertyDescriptor.ComponentType];
            foreach (IKey key in efEntityType.GetKeys())
                for (int i = 0; i < key.Properties.Count; i++)
                    if (key.Properties[i].Name == propertyDescriptor.Name)
                        return i;

            foreach (IForeignKey fkey in efEntityType.GetForeignKeys())
            {
                IReadOnlyList<IProperty> structuralProperties = fkey.PrincipalEntityType == efEntityType ? fkey.PrincipalKey.Properties : fkey.Properties;
                for (int i = 0; i < structuralProperties.Count; i++)
                    if (structuralProperties[i].Name == propertyDescriptor.Name)
                        return i;
            }

            return -1;
        }
        private static PropertyDescriptor[] GetPropertyDescriptors(IReadOnlyList<IProperty> structuralProperties)
        {
            var propertyDescriptors = new PropertyDescriptor[structuralProperties.Count];
            for (int i = 0; i < structuralProperties.Count; i++)
                propertyDescriptors[i] = TypeDescriptor.GetProperties(structuralProperties[i].PropertyInfo.DeclaringType)[structuralProperties[i].Name];
            return propertyDescriptors;
        }
        public override bool IsKey(PropertyDescriptor propertyDescriptor)
        {
            foreach (IKey key in _entityTypes[propertyDescriptor.ComponentType].GetKeys())
                for (int i = 0; i < key.Properties.Count; i++)
                    if (key.Properties[i].Name == propertyDescriptor.Name)
                        return true;
            return false;
        }
        public override bool IsNotMapped(PropertyDescriptor propertyDescriptor)
        {
            IEntityType efEntityType = _entityTypes[propertyDescriptor.ComponentType];
            foreach (IProperty efProperty in efEntityType.GetProperties())
                if (efProperty.Name == propertyDescriptor.Name)
                    return false;

            foreach (IForeignKey fkey in efEntityType.GetForeignKeys())
            {
                INavigation navigationProperty = fkey.PrincipalEntityType == efEntityType ? fkey.PrincipalToDependent : fkey.DependentToPrincipal;
                if (navigationProperty.Name == propertyDescriptor.Name)
                    return false;


                if (fkey.PrincipalEntityType == fkey.DeclaringEntityType)
                    if (fkey.DependentToPrincipal.Name == propertyDescriptor.Name)
                        return false;
            }

            foreach (IForeignKey fkey in efEntityType.GetReferencingForeignKeys())
                if (fkey.PrincipalToDependent.Name == propertyDescriptor.Name)
                    return false;

            return true;
        }
    }
}
