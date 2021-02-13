using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace OdataToEntity.ModelBuilder
{
    public static class OeModelBuilderHelper
    {
        public static PropertyInfo? GetConventionalKeyProperty(Type clrType)
        {
            PropertyInfo? key = clrType.GetPropertyIgnoreCaseOrNull("id");
            if (key != null)
                return key;

            key = clrType.GetPropertyIgnoreCaseOrNull(clrType.Name + "id");
            if (key != null)
                return key;

            return clrType.GetPropertyIgnoreCaseOrNull(clrType.Name + "_id");
        }
        public static IReadOnlyList<PropertyInfo> GetKeyProperties(Type entityType)
        {
            List<PropertyInfo>? keys = null;
            foreach (PropertyInfo property in entityType.GetProperties())
                if (property.IsDefined(typeof(KeyAttribute)))
                {
                    if (keys == null)
                        keys = new List<PropertyInfo>();

                    keys.Add(property);
                }

            if (keys != null)
                return keys;

            PropertyInfo? key = GetConventionalKeyProperty(entityType);
            return key == null ? Array.Empty<PropertyInfo>() : new[] { key };
        }
    }
}
