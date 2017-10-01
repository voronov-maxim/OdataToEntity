using Microsoft.OData;
using Microsoft.OData.Edm;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace OdataToEntity.ModelBuilder
{
    public static class OeEdmClrHelper
    {
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
                PropertyInfo clrProperty = clrType.GetTypeInfo().GetProperty(edmProperty.Name);
                clrProperty.SetValue(instance, GetValue(edmModel, edmProperty.Value));
            }
            return instance;
        }
        public static Object GetValue(IEdmModel edmModel, Object odataValue)
        {
            if (odataValue == null)
                return null;

            if (odataValue is ODataEnumValue)
                return GetValue(edmModel, odataValue as ODataEnumValue);

            if (odataValue is ODataCollectionValue)
                return GetValue(edmModel, odataValue as ODataCollectionValue);

            if (odataValue is ODataResource)
                return GetValue(edmModel, odataValue as ODataResource);

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
