using OdataToEntity.Parsers;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;

namespace OdataToEntity.ModelBuilder
{
    public class OeEdmModelMetadataProvider
    {
        public virtual PropertyInfo[] GetForeignKey(PropertyInfo propertyInfo)
        {
            var fkey = (ForeignKeyAttribute)propertyInfo.GetCustomAttribute(typeof(ForeignKeyAttribute));
            if (fkey == null)
                return null;

            PropertyInfo property = propertyInfo.DeclaringType.GetPropertyIgnoreCase(fkey.Name);
            if (property == null)
            {
                String[] propertyNames = fkey.Name.Split(',');
                if (propertyNames.Length == 1)
                    throw new InvalidOperationException("property " + fkey.Name + " foreign key " + propertyInfo.Name + " not found");

                var properties = new PropertyInfo[propertyNames.Length];
                for (int i = 0; i < properties.Length; i++)
                {
                    String propertyName = propertyNames[i].Trim();
                    property = propertyInfo.DeclaringType.GetPropertyIgnoreCase(propertyName);
                    if (property == null)
                        throw new InvalidOperationException("property " + propertyName + " foreign key " + propertyInfo.Name + " not found");

                    properties[i] = property;
                }
                return properties;
            }

            return new PropertyInfo[] { property };
        }
        public virtual PropertyInfo GetInverseProperty(PropertyInfo propertyInfo)
        {
            var inverse = (InversePropertyAttribute)propertyInfo.GetCustomAttribute(typeof(InversePropertyAttribute));
            if (inverse == null)
                return null;

            Type clrType = OeExpressionHelper.GetCollectionItemType(propertyInfo.PropertyType);
            if (clrType == null)
                clrType = propertyInfo.PropertyType;

            return clrType.GetPropertyIgnoreCase(inverse.Property);
        }
        public virtual int GetOrder(PropertyInfo propertyInfo)
        {
            var column = (ColumnAttribute)propertyInfo.GetCustomAttribute(typeof(ColumnAttribute));
            return column == null ? -1 : column.Order;
        }
        public virtual bool IsKey(PropertyInfo propertyInfo)
        {
            return propertyInfo.GetCustomAttribute(typeof(KeyAttribute)) != null;
        }
        public virtual bool IsNotMapped(PropertyInfo propertyInfo)
        {
            return propertyInfo.GetCustomAttribute(typeof(NotMappedAttribute)) != null;
        }
    }
}
