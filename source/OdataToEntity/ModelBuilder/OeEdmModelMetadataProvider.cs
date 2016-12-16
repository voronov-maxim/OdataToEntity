using OdataToEntity.Parsers;
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OdataToEntity.ModelBuilder
{
    public class OeEdmModelMetadataProvider
    {
        public virtual PropertyDescriptor GetForeignKey(PropertyDescriptor propertyDescriptor)
        {
            var fkey = (ForeignKeyAttribute)propertyDescriptor.Attributes[typeof(ForeignKeyAttribute)];
            return fkey == null ? null : TypeDescriptor.GetProperties(propertyDescriptor.ComponentType).Find(fkey.Name, true);
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
