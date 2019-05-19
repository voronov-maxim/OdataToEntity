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
                    property = propertyInfo.DeclaringType.GetPropertyIgnoreCase(propertyName) ??
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

            Type clrType = OeExpressionHelper.GetCollectionItemTypeOrNull(propertyInfo.PropertyType) ?? propertyInfo.PropertyType;
            return clrType.GetPropertyIgnoreCase(inverse.Property);
        }
        public virtual int GetOrder(PropertyInfo propertyInfo)
        {
            var column = (ColumnAttribute)propertyInfo.GetCustomAttribute(typeof(ColumnAttribute));
            return column == null ? -1 : column.Order;
        }
        public virtual PropertyInfo[] GetPrincipalToDependentWithoutDependent(PropertyInfo propertyInfo)
        {
            Type itemType = OeExpressionHelper.GetCollectionItemTypeOrNull(propertyInfo.PropertyType);
            if (itemType != null && GetInverseProperty(propertyInfo) == null)
            { 
                String dependentPropertyName = propertyInfo.DeclaringType.Name + "id";
                return new PropertyInfo[] { itemType.GetPropertyIgnoreCase(dependentPropertyName) };
            }
            return null;
        }
        public virtual PropertyInfo[] GetProperties(Type type)
        {
            return type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
        }
        public virtual bool IsKey(PropertyInfo propertyInfo)
        {
            return propertyInfo.GetCustomAttribute(typeof(KeyAttribute)) != null;
        }
        public virtual bool IsNotMapped(PropertyInfo propertyInfo)
        {
            return propertyInfo.GetCustomAttribute(typeof(NotMappedAttribute)) != null;
        }
        public virtual bool IsRequired(PropertyInfo propertyInfo)
        {
            return !PrimitiveTypeHelper.IsNullable(propertyInfo.PropertyType) ||
                propertyInfo.GetCustomAttribute(typeof(RequiredAttribute)) != null ||
                IsKey(propertyInfo);
        }
        public void SortClrPropertyByOrder(PropertyInfo[] clrProperties)
        {
            if (clrProperties.Length > 1)
                Array.Sort(clrProperties, (x, y) => GetOrder(x).CompareTo(GetOrder(y)));
        }
    }
}
