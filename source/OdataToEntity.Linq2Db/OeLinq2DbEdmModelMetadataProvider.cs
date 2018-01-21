using OdataToEntity.ModelBuilder;
using System;
using OdataToEntity.Parsers;
using LinqToDB.Mapping;
using System.Reflection;

namespace OdataToEntity.Linq2Db
{
    public sealed class OeLinq2DbEdmModelMetadataProvider : OeEdmModelMetadataProvider
    {
        public override PropertyInfo[] GetForeignKey(PropertyInfo propertyInfo)
        {
            var association = (AssociationAttribute)propertyInfo.GetCustomAttribute(typeof(AssociationAttribute));
            if (association == null || association.IsBackReference)
                return null;

            PropertyInfo property = propertyInfo.DeclaringType.GetPropertyIgnoreCase(association.ThisKey);
            if (property == null)
            {
                String[] propertyNames = association.GetThisKeys();
                if (propertyNames.Length == 1)
                    throw new InvalidOperationException("property " + association.KeyName + " foreign key " + propertyInfo.Name + " not found");

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
        public override PropertyInfo GetInverseProperty(PropertyInfo propertyInfo)
        {
            var association = (AssociationAttribute)propertyInfo.GetCustomAttribute(typeof(AssociationAttribute));
            if (association == null || !association.IsBackReference)
                return null;

            Type clrType = OeExpressionHelper.GetCollectionItemType(propertyInfo.PropertyType);
            if (clrType == null)
                clrType = propertyInfo.PropertyType;

            foreach (PropertyInfo clrProperty in clrType.GetProperties())
            {
                var association2 = (AssociationAttribute)clrProperty.GetCustomAttribute(typeof(AssociationAttribute));
                if (association2 != null && association2.ThisKey == association.OtherKey)
                    return clrProperty;
            }

            return null;
        }
        public override int GetOrder(PropertyInfo propertyInfo)
        {
            var key = (PrimaryKeyAttribute)propertyInfo.GetCustomAttribute(typeof(PrimaryKeyAttribute));
            return key == null ? -1 : key.Order;
        }
        public override bool IsKey(PropertyInfo propertyInfo)
        {
            return propertyInfo.GetCustomAttribute(typeof(PrimaryKeyAttribute)) != null;
        }
        public override bool IsNotMapped(PropertyInfo propertyInfo)
        {
            if (propertyInfo.GetCustomAttribute(typeof(ColumnAttribute)) != null)
                return false;
            if (propertyInfo.GetCustomAttribute(typeof(PrimaryKeyAttribute)) != null)
                return false;
            if (propertyInfo.GetCustomAttribute(typeof(AssociationAttribute)) != null)
                return false;

            return true;
        }
        public override bool IsRequired(PropertyInfo propertyInfo)
        {
            return IsRequiredLinq2Db(propertyInfo);
        }
        internal static bool IsRequiredLinq2Db(PropertyInfo propertyInfo)
        {
            Type clrType = propertyInfo.PropertyType;
            bool isNullable = clrType.IsClass || (clrType.IsGenericType && clrType.GetGenericTypeDefinition() == typeof(Nullable<>));
            return !isNullable || propertyInfo.GetCustomAttribute(typeof(NotNullAttribute)) != null;
        }
    }
}
