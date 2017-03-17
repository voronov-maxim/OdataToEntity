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
        public static Type GetClrType(IEdmModel edmModel, IEdmType edmType)
        {
            if (edmType.TypeKind == EdmTypeKind.Primitive)
                return PrimitiveTypeHelper.GetClrType((edmType as IEdmPrimitiveType).PrimitiveKind);

            OeClrTypeAnnotation clrTypeAnnotation = edmModel.GetAnnotationValue<OeClrTypeAnnotation>(edmType);
            if (clrTypeAnnotation != null)
                return clrTypeAnnotation.ClrType;

            if (edmType.TypeKind == EdmTypeKind.Collection)
                return GetClrType(edmModel, (edmType as IEdmCollectionType).ElementType.Definition);

            throw new InvalidOperationException("Add OeClrTypeAnnotation for " + edmType.FullTypeName());
        }
        public static Object GetValue(IEdmModel edmModel, ODataEnumValue odataValue)
        {
            IEdmSchemaType schemaType = edmModel.FindType(odataValue.TypeName);
            Type clrType = GetClrType(edmModel, (IEdmEnumType)schemaType.AsElementType());
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
            Type clrType = GetClrType(edmModel, edmModel.FindType(resource.TypeName));
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

    }
}
