using OdataToEntity.Parsers;
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OdataToEntity.ModelBuilder
{
    public class OeEdmModelMetadataProvider
    {
        public virtual PropertyDescriptor[] GetForeignKey(PropertyDescriptor propertyDescriptor)
        {
            var fkey = (ForeignKeyAttribute)propertyDescriptor.Attributes[typeof(ForeignKeyAttribute)];
            if (fkey == null)
                return null;

            PropertyDescriptor property = TypeDescriptor.GetProperties(propertyDescriptor.ComponentType).Find(fkey.Name, true);
            if (property == null)
            {
                String[] propertyNames = fkey.Name.Split(',');
                if (propertyNames.Length == 1)
                    throw new InvalidOperationException("property " + fkey.Name + " foreign key " + propertyDescriptor.Name + " not found");

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
        public virtual PropertyDescriptor GetInverseProperty(PropertyDescriptor propertyDescriptor)
        {
            var inverse = (InversePropertyAttribute)propertyDescriptor.Attributes[typeof(InversePropertyAttribute)];
            if (inverse == null)
                return null;

            Type type = OeExpressionHelper.GetCollectionItemType(propertyDescriptor.PropertyType);
            if (type == null)
                type = propertyDescriptor.PropertyType;

            return TypeDescriptor.GetProperties(type)[inverse.Property];
        }
        public virtual int GetOrder(PropertyDescriptor propertyDescriptor)
        {
            var column = (ColumnAttribute)propertyDescriptor.Attributes[typeof(ColumnAttribute)];
            return column == null ? -1 : column.Order;
        }
        public virtual bool IsKey(PropertyDescriptor propertyDescriptor)
        {
            return propertyDescriptor.Attributes[typeof(KeyAttribute)] != null;
        }
        public virtual bool IsNotMapped(PropertyDescriptor propertyDescriptor)
        {
            return propertyDescriptor.Attributes[typeof(NotMappedAttribute)] != null;
        }
    }
}
