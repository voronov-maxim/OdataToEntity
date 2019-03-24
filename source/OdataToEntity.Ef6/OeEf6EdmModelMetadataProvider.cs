using OdataToEntity.ModelBuilder;
using System;
using System.Collections.Generic;
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

        private IEnumerable<EntityType> GetEntityTypes(PropertyInfo propertyInfo)
        {
            if (_entityTypes.TryGetValue(propertyInfo.DeclaringType, out EntityType efEntityType))
                yield return efEntityType;
            else
                foreach (KeyValuePair<Type, EntityType> pair in _entityTypes)
                    if (propertyInfo.DeclaringType.IsAssignableFrom(pair.Key))
                        yield return pair.Value;
        }
        public override PropertyInfo[] GetForeignKey(PropertyInfo propertyInfo)
        {
            foreach (EntityType efEntityType in GetEntityTypes(propertyInfo))
                foreach (NavigationProperty navigationProperty in efEntityType.NavigationProperties)
                {
                    if (!navigationProperty.GetDependentProperties().Any())
                        continue;

                    ReferentialConstraint refConstraint = ((AssociationType)navigationProperty.RelationshipType).Constraint;
                    if (navigationProperty.Name == propertyInfo.Name)
                    {
                        var properties = new PropertyInfo[refConstraint.ToProperties.Count];
                        for (int i = 0; i < refConstraint.ToProperties.Count; i++)
                            properties[i] = propertyInfo.DeclaringType.GetPropertyIgnoreCase(refConstraint.ToProperties[i].Name);
                        return properties;
                    }

                    for (int i = 0; i < refConstraint.ToProperties.Count; i++)
                        if (refConstraint.ToProperties[i].Name == propertyInfo.Name)
                            return new PropertyInfo[] { propertyInfo.DeclaringType.GetPropertyIgnoreCase(navigationProperty.Name) };
                }

            return null;
        }
        public override PropertyInfo GetInverseProperty(PropertyInfo propertyInfo)
        {
            foreach (EntityType efEntityType in GetEntityTypes(propertyInfo))
                foreach (NavigationProperty navigationProperty in efEntityType.NavigationProperties)
                    if (navigationProperty.Name == propertyInfo.Name)
                    {
                        if (!navigationProperty.ToEndMember.MetadataProperties.TryGetValue("ClrPropertyInfo", false, out MetadataProperty metadataProperty))
                            return null;

                        var inverseProperty = (PropertyInfo)metadataProperty.Value;
                        return inverseProperty.DeclaringType.GetPropertyIgnoreCase(inverseProperty.Name);
                    }

            return null;
        }
        public override int GetOrder(PropertyInfo propertyInfo)
        {
            foreach (EntityType efEntityType in GetEntityTypes(propertyInfo))
            {
                for (int i = 0; i < efEntityType.KeyProperties.Count; i++)
                    if (efEntityType.KeyProperties[i].Name == propertyInfo.Name)
                        return i;

                for (int i = 0; i < efEntityType.NavigationProperties.Count; i++)
                {
                    int index = 0;
                    foreach (EdmProperty edmProperty in efEntityType.NavigationProperties[i].GetDependentProperties())
                    {
                        if (edmProperty.Name == propertyInfo.Name)
                            return index;
                        index++;
                    }
                }
            }

            return -1;
        }
        public override bool IsKey(PropertyInfo propertyInfo)
        {
            foreach (EntityType efEntityType in GetEntityTypes(propertyInfo))
                for (int i = 0; i < efEntityType.KeyProperties.Count; i++)
                    if (efEntityType.KeyProperties[i].Name == propertyInfo.Name)
                        return true;
            return false;
        }
        public override bool IsNotMapped(PropertyInfo propertyInfo)
        {
            foreach (EntityType efEntityType in GetEntityTypes(propertyInfo))
            {
                for (int i = 0; i < efEntityType.Properties.Count; i++)
                    if (efEntityType.Properties[i].Name == propertyInfo.Name)
                        return false;

                for (int i = 0; i < efEntityType.NavigationProperties.Count; i++)
                    if (efEntityType.NavigationProperties[i].Name == propertyInfo.Name)
                        return false;
            }

            return true;
        }
        public override bool IsRequired(PropertyInfo propertyInfo)
        {
            foreach (EntityType efEntityType in GetEntityTypes(propertyInfo))
            {
                for (int i = 0; i < efEntityType.Properties.Count; i++)
                    if (efEntityType.Properties[i].Name == propertyInfo.Name)
                        return !efEntityType.Properties[i].Nullable;

                for (int i = 0; i < efEntityType.NavigationProperties.Count; i++)
                {
                    NavigationProperty efProperty = efEntityType.NavigationProperties[i];
                    if (efProperty.Name == propertyInfo.Name)
                        return efProperty.FromEndMember.RelationshipMultiplicity == RelationshipMultiplicity.One ||
                            efProperty.ToEndMember.RelationshipMultiplicity == RelationshipMultiplicity.One;
                }
            }

            throw new InvalidOperationException("property " + propertyInfo.Name + " not found");
        }
    }
}
