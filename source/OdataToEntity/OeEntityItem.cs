using Microsoft.OData;
using Microsoft.OData.Edm;
using System;
using System.ComponentModel;
using System.Reflection;

namespace OdataToEntity
{
    public sealed class OeEntityItem
    {
        private readonly Object _entity;
        private readonly ODataResource _entry;
        private readonly IEdmEntitySet _entitySet;
        private readonly IEdmEntityType _entityType;
        private readonly Type _clrType;

        public OeEntityItem(IEdmEntitySet entitySet, IEdmEntityType entityType, Type clrType, ODataResource entry)
        {
            _entitySet = entitySet;
            _entityType = entityType;
            _clrType = clrType;
            _entry = entry;

            _entity = CreateEntity(clrType, entry);
        }

        public static Object CreateEntity(Type clrType, ODataResource entry)
        {
            Object entity = Activator.CreateInstance(clrType);

            PropertyDescriptorCollection clrProperties = TypeDescriptor.GetProperties(clrType);
            foreach (ODataProperty property in entry.Properties)
            {
                PropertyDescriptor clrProperty = clrProperties[property.Name];
                if (clrProperty != null)
                {
                    if (property.Value is ODataEnumValue)
                    {
                        Type enumType;
                        if (clrProperty.PropertyType.GetTypeInfo().IsEnum)
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
        public void RefreshEntry()
        {
            PropertyDescriptorCollection clrProperties = TypeDescriptor.GetProperties(_clrType);
            foreach (ODataProperty property in _entry.Properties)
            {
                PropertyDescriptor clrProperty = clrProperties[property.Name];
                if (clrProperty != null)
                {
                    Object value = clrProperty.GetValue(_entity);
                    if (value != null && (clrProperty.PropertyType == typeof(DateTime) || clrProperty.PropertyType == typeof(DateTime?)))
                        value = new DateTimeOffset(((DateTime)value));
                    property.Value = value;
                }
            }
        }

        public Object Entity
        {
            get
            {
                return _entity;
            }
        }
        public ODataResource Entry
        {
            get
            {
                return _entry;
            }
        }
        public IEdmEntitySet EntitySet
        {
            get
            {
                return _entitySet;
            }
        }
        public IEdmEntityType EntityType
        {
            get
            {
                return _entityType;
            }
        }
    }
}
