using OdataToEntity.ModelBuilder;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Entity;
using System.Data.Entity.Core.Metadata.Edm;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Reflection;

namespace OdataToEntity.Ef6
{
    public sealed class OeEf6EdmModelMetadataProvider : OeEdmModelMetadataProvider
    {
        private readonly Dictionary<Type, EntityType> _entityTypes;

        public OeEf6EdmModelMetadataProvider(DbContext dbContext)
        {
            MetadataWorkspace workspace = ((IObjectContextAdapter)dbContext).ObjectContext.MetadataWorkspace;
            var itemCollection = (ObjectItemCollection)workspace.GetItemCollection(DataSpace.OSpace);
            _entityTypes = workspace.GetItems<EntityType>(DataSpace.CSpace).ToDictionary(e => itemCollection.GetClrType(workspace.GetObjectSpaceType(e)));
        }

        private IEnumerable<EntityType> GetEntityTypes(PropertyDescriptor propertyDescriptor)
        {
            EntityType efEntityType;
            if (_entityTypes.TryGetValue(propertyDescriptor.ComponentType, out efEntityType))
                yield return efEntityType;
            else
                foreach (KeyValuePair<Type, EntityType> pair in _entityTypes)
                    if (propertyDescriptor.ComponentType.IsAssignableFrom(pair.Key))
                        yield return pair.Value;
        }
        public override PropertyDescriptor[] GetForeignKey(PropertyDescriptor propertyDescriptor)
        {
            foreach (EntityType efEntityType in GetEntityTypes(propertyDescriptor))
                foreach (NavigationProperty navigationProperty in efEntityType.NavigationProperties)
                {
                    if (!navigationProperty.GetDependentProperties().Any())
                        continue;

                    ReferentialConstraint refConstraint = ((AssociationType)navigationProperty.RelationshipType).Constraint;
                    if (navigationProperty.Name == propertyDescriptor.Name)
                    {
                        PropertyDescriptorCollection clrProperties = TypeDescriptor.GetProperties(propertyDescriptor.ComponentType);
                        var propertyDescriptors = new PropertyDescriptor[refConstraint.ToProperties.Count];
                        for (int i = 0; i < refConstraint.ToProperties.Count; i++)
                            propertyDescriptors[i] = clrProperties[refConstraint.ToProperties[i].Name];
                        return propertyDescriptors;
                    }

                    for (int i = 0; i < refConstraint.ToProperties.Count; i++)
                        if (refConstraint.ToProperties[i].Name == propertyDescriptor.Name)
                            return new PropertyDescriptor[] { TypeDescriptor.GetProperties(propertyDescriptor.ComponentType)[navigationProperty.Name] };
                }

            return null;
        }
        public override PropertyDescriptor GetInverseProperty(PropertyDescriptor propertyDescriptor)
        {
            foreach (EntityType efEntityType in GetEntityTypes(propertyDescriptor))
                foreach (NavigationProperty navigationProperty in efEntityType.NavigationProperties)
                    if (navigationProperty.Name == propertyDescriptor.Name)
                    {
                        MetadataProperty metadataProperty;
                        if (!navigationProperty.ToEndMember.MetadataProperties.TryGetValue("ClrPropertyInfo", false, out metadataProperty))
                            return null;

                        var inverseProperty = (PropertyInfo)metadataProperty.Value;
                        return TypeDescriptor.GetProperties(inverseProperty.DeclaringType)[inverseProperty.Name];
                    }

            return null;
        }
        public override int GetOrder(PropertyDescriptor propertyDescriptor)
        {
            foreach (EntityType efEntityType in GetEntityTypes(propertyDescriptor))
            {
                for (int i = 0; i < efEntityType.KeyProperties.Count; i++)
                    if (efEntityType.KeyProperties[i].Name == propertyDescriptor.Name)
                        return i;

                for (int i = 0; i < efEntityType.NavigationProperties.Count; i++)
                {
                    int index = 0;
                    foreach (EdmProperty edmProperty in efEntityType.NavigationProperties[i].GetDependentProperties())
                    {
                        if (edmProperty.Name == propertyDescriptor.Name)
                            return index;
                        index++;
                    }
                }
            }

            return -1;
        }
        public override bool IsKey(PropertyDescriptor propertyDescriptor)
        {
            foreach (EntityType efEntityType in GetEntityTypes(propertyDescriptor))
                for (int i = 0; i < efEntityType.KeyProperties.Count; i++)
                    if (efEntityType.KeyProperties[i].Name == propertyDescriptor.Name)
                        return true;
            return false;
        }
        public override bool IsNotMapped(PropertyDescriptor propertyDescriptor)
        {
            foreach (EntityType efEntityType in GetEntityTypes(propertyDescriptor))
            {
                for (int i = 0; i < efEntityType.Properties.Count; i++)
                    if (efEntityType.Properties[i].Name == propertyDescriptor.Name)
                        return false;

                for (int i = 0; i < efEntityType.NavigationProperties.Count; i++)
                    if (efEntityType.NavigationProperties[i].Name == propertyDescriptor.Name)
                        return false;
            }

            return true;
        }
    }
}
