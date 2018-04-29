using Microsoft.OData;
using Microsoft.OData.Edm;
using OdataToEntity.ModelBuilder;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace OdataToEntity
{
    public static class OeEdmClrHelper
    {
        public static Object CreateEntity(Type clrType, ODataResourceBase entry)
        {
            Object entity = Activator.CreateInstance(clrType);
            foreach (ODataProperty property in entry.Properties)
            {
                PropertyInfo clrProperty = clrType.GetProperty(property.Name);
                if (clrProperty != null)
                {
                    Object value = GetClrValue(clrProperty.PropertyType, property.Value);
                    clrProperty.SetValue(entity, value);
                }
            }
            return entity;
        }
        public static ODataValue CreateODataValue(Object value)
        {
            if (value == null)
                return new ODataNullValue();

            if (value.GetType().IsEnum)
                return new ODataEnumValue(value.ToString());

            if (value is DateTime dateTime)
            {
                DateTimeOffset dateTimeOffset;
                switch (dateTime.Kind)
                {
                    case DateTimeKind.Unspecified:
                        dateTimeOffset = new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc));
                        break;
                    case DateTimeKind.Utc:
                        dateTimeOffset = new DateTimeOffset(dateTime);
                        break;
                    case DateTimeKind.Local:
                        dateTimeOffset = new DateTimeOffset(dateTime.ToUniversalTime());
                        break;
                    default:
                        throw new ArgumentOutOfRangeException("unknown DateTimeKind " + dateTime.Kind.ToString());
                }
                return new ODataPrimitiveValue(dateTimeOffset);
            }

            return new ODataPrimitiveValue(value);
        }
        public static Type GetClrType(this IEdmModel edmModel, IEdmType edmType)
        {
            if (edmType.TypeKind == EdmTypeKind.Primitive)
                return PrimitiveTypeHelper.GetClrType((edmType as IEdmPrimitiveType).PrimitiveKind);

            OeValueAnnotation<Type> clrTypeAnnotation = edmModel.GetAnnotationValue<OeValueAnnotation<Type>>(edmType);
            if (clrTypeAnnotation != null)
                return clrTypeAnnotation.Value;

            if (edmType.TypeKind == EdmTypeKind.Collection)
                return edmModel.GetClrType((edmType as IEdmCollectionType).ElementType.Definition);

            throw new InvalidOperationException("Add OeClrTypeAnnotation for " + edmType.FullTypeName());
        }
        public static Object GetClrValue(Type clrType, Object edmValue)
        {
            if (edmValue is ODataEnumValue enumValue)
            {
                Type enumType;
                if (clrType.IsEnum)
                    enumType = clrType;
                else
                    enumType = Nullable.GetUnderlyingType(clrType);
                return Enum.Parse(enumType, enumValue.Value);
            }

            if (edmValue is DateTimeOffset dateTimeOffset && (clrType == typeof(DateTime) || clrType == typeof(DateTime?)))
                return dateTimeOffset.UtcDateTime;

            return edmValue;
        }
        public static IEdmTypeReference GetEdmTypeReference(this IEdmModel edmModel, Type clrType)
        {
            IEdmTypeReference edmTypeRef;
            bool nullable;
            Type underlyingType = Nullable.GetUnderlyingType(clrType);
            if (underlyingType == null)
                nullable = clrType.IsClass;
            else
            {
                clrType = underlyingType;
                nullable = true;
            }

            IEdmPrimitiveType primitiveEdmType = PrimitiveTypeHelper.GetPrimitiveType(clrType);
            if (primitiveEdmType == null)
            {
                var edmEnumType = (IEdmEnumType)edmModel.FindType(clrType.FullName);
                edmTypeRef = new EdmEnumTypeReference(edmEnumType, nullable);
            }
            else
                edmTypeRef = EdmCoreModel.Instance.GetPrimitive(primitiveEdmType.PrimitiveKind, nullable);

            return edmTypeRef;
        }
        public static IEdmEntitySet GetEntitySet(IEdmModel edmModel, Type clrType)
        {
            String fullName = clrType.FullName;
            foreach (IEdmEntitySet element in edmModel.EntityContainer.EntitySets())
                if (String.CompareOrdinal(element.EntityType().FullTypeName(), fullName) == 0)
                    return element;
            return null;
        }
        public static IEdmEntitySet GetEntitySet(IEdmModel edmModel, IEdmType edmType)
        {
            foreach (IEdmEntitySet element in edmModel.EntityContainer.EntitySets())
                if (element.Type == edmType)
                    return element;
            return null;
        }
        public static PropertyInfo GetPropertyIgnoreCase(this Type declaringType, String propertyName)
        {
            return declaringType.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        }
        public static Object GetValue(IEdmModel edmModel, ODataEnumValue odataValue)
        {
            IEdmSchemaType schemaType = edmModel.FindType(odataValue.TypeName);
            Type clrType = edmModel.GetClrType((IEdmEnumType)schemaType.AsElementType());
            return Enum.Parse(clrType, odataValue.Value);
        }
        public static Object GetValue(IEdmModel edmModel, ODataCollectionValue odataValue)
        {
            var collection = odataValue as ODataCollectionValue;
            IList list = null;
            foreach (Object odataItem in collection.Items)
            {
                Object listItem = GetValue(edmModel, odataItem);
                if (list == null)
                {
                    Type listType = typeof(List<>).MakeGenericType(new[] { listItem.GetType() });
                    list = (IList)Activator.CreateInstance(listType);
                }
                list.Add(listItem);
            }
            return list;
        }
        public static Object GetValue(IEdmModel edmModel, ODataResource odataValue)
        {
            var resource = odataValue as ODataResource;
            Type clrType = edmModel.GetClrType(edmModel.FindType(resource.TypeName));
            Object instance = Activator.CreateInstance(clrType);
            foreach (ODataProperty edmProperty in resource.Properties)
            {
                PropertyInfo clrProperty = clrType.GetProperty(edmProperty.Name);
                if (clrProperty.PropertyType == typeof(DateTime) || clrProperty.PropertyType == typeof(DateTime?))
                    clrProperty.SetValue(instance, ((DateTimeOffset)edmProperty.Value).UtcDateTime);
                else
                    clrProperty.SetValue(instance, GetValue(edmModel, edmProperty.Value));
            }
            return instance;
        }
        public static Object GetValue(IEdmModel edmModel, Object odataValue)
        {
            if (odataValue == null)
                return null;

            if (odataValue is ODataEnumValue enumValue)
                return GetValue(edmModel, enumValue);

            if (odataValue is ODataCollectionValue collectionValue)
                return GetValue(edmModel, collectionValue);

            if (odataValue is ODataResource resourceValue)
                return GetValue(edmModel, resourceValue);

            return odataValue;
        }
        public static bool IsDbFunction(this IEdmModel edmModel, IEdmOperation edmOperation)
        {
            OeValueAnnotation<bool> valueAnnotation = edmModel.GetAnnotationValue<OeValueAnnotation<bool>>(edmOperation);
            return valueAnnotation.Value;
        }
        public static void SetClrType(this IEdmModel edmModel, IEdmType edmType, Type clrType)
        {
            edmModel.SetAnnotationValue(edmType, new OeValueAnnotation<Type>(clrType));
        }
        public static void SetIsDbFunction(this IEdmModel edmModel, IEdmOperation edmOperation, bool isDbFunction)
        {
            edmModel.SetAnnotationValue(edmOperation, new OeValueAnnotation<bool>(isDbFunction));
        }
    }
}
