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

        private IEnumerable<IEntityType> GetEntityTypes(PropertyDescriptor propertyDescriptor)
        {
            IEntityType efEntityType;
            if (_entityTypes.TryGetValue(propertyDescriptor.ComponentType, out efEntityType))
                yield return efEntityType;
            else
                foreach (KeyValuePair<Type, IEntityType> pair in _entityTypes)
                    if (propertyDescriptor.ComponentType.IsAssignableFrom(pair.Key))
                        yield return pair.Value;
        }
        public override PropertyDescriptor[] GetForeignKey(PropertyDescriptor propertyDescriptor)
        {
            foreach (IEntityType efEntityType in GetEntityTypes(propertyDescriptor))
                foreach (IForeignKey fkey in efEntityType.GetForeignKeys())
                {
                    if (fkey.PrincipalEntityType == efEntityType && fkey.PrincipalEntityType != fkey.DeclaringEntityType)
                        continue;

                    if (fkey.DependentToPrincipal.Name == propertyDescriptor.Name)
                    {
                        PropertyDescriptorCollection clrProperties = TypeDescriptor.GetProperties(propertyDescriptor.ComponentType);
                        var propertyDescriptors = new PropertyDescriptor[fkey.Properties.Count];
                        for (int i = 0; i < fkey.Properties.Count; i++)
                            propertyDescriptors[i] = clrProperties[fkey.Properties[i].Name];
                        return propertyDescriptors;
                    }

                    for (int i = 0; i < fkey.Properties.Count; i++)
                        if (fkey.Properties[i].Name == propertyDescriptor.Name)
                            return new PropertyDescriptor[] { TypeDescriptor.GetProperties(propertyDescriptor.ComponentType)[fkey.DependentToPrincipal.Name] };
                }

            return null;
        }
        public override PropertyDescriptor GetInverseProperty(PropertyDescriptor propertyDescriptor)
        {
            foreach (IEntityType efEntityType in GetEntityTypes(propertyDescriptor))
            {
                foreach (IForeignKey fkey in efEntityType.GetForeignKeys())
                    if (fkey.DependentToPrincipal.Name == propertyDescriptor.Name)
                    {
                        INavigation inverseProperty = fkey.DependentToPrincipal.FindInverse();
                        return inverseProperty == null ? null : TypeDescriptor.GetProperties(inverseProperty.DeclaringEntityType.ClrType)[inverseProperty.Name];
                    }

                foreach (IForeignKey fkey in efEntityType.GetReferencingForeignKeys())
                    if (fkey.PrincipalToDependent != null && fkey.PrincipalToDependent.Name == propertyDescriptor.Name)
                    {
                        INavigation inverseProperty = fkey.PrincipalToDependent.FindInverse();
                        return inverseProperty == null ? null : TypeDescriptor.GetProperties(inverseProperty.DeclaringEntityType.ClrType)[inverseProperty.Name];
                    }
            }

            return null;
        }
        public override int GetOrder(PropertyDescriptor propertyDescriptor)
        {
            foreach (IEntityType efEntityType in GetEntityTypes(propertyDescriptor))
            {
                foreach (IKey key in efEntityType.GetKeys())
                    for (int i = 0; i < key.Properties.Count; i++)
                        if (key.Properties[i].Name == propertyDescriptor.Name)
                            return i;

                foreach (IForeignKey fkey in efEntityType.GetForeignKeys())
                    for (int i = 0; i < fkey.Properties.Count; i++)
                        if (fkey.Properties[i].Name == propertyDescriptor.Name)
                            return i;
            }

            return -1;
        }
        public override bool IsKey(PropertyDescriptor propertyDescriptor)
        {
            foreach (IEntityType efEntityType in GetEntityTypes(propertyDescriptor))
                foreach (IKey key in efEntityType.GetKeys())
                    for (int i = 0; i < key.Properties.Count; i++)
                        if (key.Properties[i].Name == propertyDescriptor.Name)
                            return true;
            return false;
        }
        public override bool IsNotMapped(PropertyDescriptor propertyDescriptor)
        {
            foreach (IEntityType efEntityType in GetEntityTypes(propertyDescriptor))
            {
                foreach (IProperty efProperty in efEntityType.GetProperties())
                    if (efProperty.Name == propertyDescriptor.Name)
                        return false;

                foreach (IForeignKey fkey in efEntityType.GetForeignKeys())
                    if (fkey.DependentToPrincipal.Name == propertyDescriptor.Name)
                        return false;

                foreach (IForeignKey fkey in efEntityType.GetReferencingForeignKeys())
                    if (fkey.PrincipalToDependent.Name == propertyDescriptor.Name)
                        return false;
            }

            return true;
        }
    }
}
