using OdataToEntity.ModelBuilder;
using System.ComponentModel;
using System;
using OdataToEntity.Parsers;
using LinqToDB.Mapping;

namespace OdataToEntity.Linq2Db
{
    public sealed class OeLinq2DbEdmModelMetadataProvider : OeEdmModelMetadataProvider
    {
        public override PropertyDescriptor[] GetForeignKey(PropertyDescriptor propertyDescriptor)
        {
            var association = (AssociationAttribute)propertyDescriptor.Attributes[typeof(AssociationAttribute)];
            if (association == null || association.IsBackReference)
                return null;

            PropertyDescriptor property = TypeDescriptor.GetProperties(propertyDescriptor.ComponentType).Find(association.ThisKey, true);
            if (property == null)
            {
                String[] propertyNames = association.GetThisKeys();
                if (propertyNames.Length == 1)
                    throw new InvalidOperationException("property " + association.KeyName + " foreign key " + propertyDescriptor.Name + " not found");

                var properties = new PropertyDescriptor[propertyNames.Length];
                for (int i = 0; i < properties.Length; i++)
                {
                    String propertyName = propertyNames[i].Trim();
                    property = TypeDescriptor.GetProperties(propertyDescriptor.ComponentType).Find(propertyName, true);
                    if (property == null)
                        throw new InvalidOperationException("property " + propertyName + " foreign key " + propertyDescriptor.Name + " not found");

                    properties[i] = property;
                }
                return properties;
            }

            return new PropertyDescriptor[] { property };
        }
        public override PropertyDescriptor GetInverseProperty(PropertyDescriptor propertyDescriptor)
        {
            var association = (AssociationAttribute)propertyDescriptor.Attributes[typeof(AssociationAttribute)];
            if (association == null || !association.IsBackReference)
                return null;

            Type type = OeExpressionHelper.GetCollectionItemType(propertyDescriptor.PropertyType);
            if (type == null)
                type = propertyDescriptor.PropertyType;

            foreach (PropertyDescriptor clrProperty in TypeDescriptor.GetProperties(type))
            {
                var association2 = (AssociationAttribute)clrProperty.Attributes[typeof(AssociationAttribute)];
                if (association2 != null && association2.ThisKey == association.OtherKey)
                    return clrProperty;
            }

            return null;
        }
        public override int GetOrder(PropertyDescriptor propertyDescriptor)
        {
            var key = (PrimaryKeyAttribute)propertyDescriptor.Attributes[typeof(PrimaryKeyAttribute)];
            return key == null ? -1 : key.Order;
        }
        public override bool IsKey(PropertyDescriptor propertyDescriptor)
        {
            return propertyDescriptor.Attributes[typeof(PrimaryKeyAttribute)] != null;
        }
        public override bool IsNotMapped(PropertyDescriptor propertyDescriptor)
        {
            if (propertyDescriptor.Attributes[typeof(ColumnAttribute)] != null)
                return false;
            if (propertyDescriptor.Attributes[typeof(PrimaryKeyAttribute)] != null)
                return false;
            if (propertyDescriptor.Attributes[typeof(AssociationAttribute)] != null)
                return false;

            return true;
        }
    }
}
