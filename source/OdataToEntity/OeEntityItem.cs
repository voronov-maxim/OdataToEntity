using Microsoft.OData;
using Microsoft.OData.Edm;
using System;
using System.Reflection;

namespace OdataToEntity
{
    public sealed class OeEntityItem
    {
        private readonly Object _entity;
        private readonly ODataResource _entry;
        private readonly IEdmEntitySet _entitySet;
        private readonly IEdmEntityType _entityType;

        public OeEntityItem(IEdmEntitySet entitySet, IEdmEntityType entityType, Type clrType, ODataResource entry)
        {
            _entitySet = entitySet;
            _entityType = entityType;
            _entry = entry;

            _entity = CreateEntity(clrType, entry);
        }

        public static Object CreateEntity(Type clrType, ODataResource entry)
        {
            Object entity = Activator.CreateInstance(clrType);

            foreach (ODataProperty property in entry.Properties)
            {
                PropertyInfo clrProperty = clrType.GetProperty(property.Name);
                if (clrProperty != null)
                {
                    if (property.Value is ODataEnumValue)
                    {
                        Type enumType;
                        if (clrProperty.PropertyType.IsEnum)
                            enumType = clrProperty.PropertyType;
                        else
                            enumType = Nullable.GetUnderlyingType(clrProperty.PropertyType);
                        var enumValue = (ODataEnumValue)property.Value;
                        Object value = Enum.Parse(enumType, enumValue.Value);
                        clrProperty.SetValue(entity, value);
                    }
                    else
                    {
                        if (property.Value != null && (clrProperty.PropertyType == typeof(DateTime) || clrProperty.PropertyType == typeof(DateTime?)))
                        {
                            DateTime dateTime = ((DateTimeOffset)property.Value).UtcDateTime;
                            clrProperty.SetValue(entity, dateTime);
                        }
                        else
                            clrProperty.SetValue(entity, property.Value);
                    }
                }
            }
            return entity;
        }

        public Object Entity => _entity;
        public ODataResource Entry => _entry;
        public IEdmEntitySet EntitySet => _entitySet;
        public IEdmEntityType EntityType => _entityType;
    }
}
