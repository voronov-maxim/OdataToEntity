using Microsoft.OData.Edm;
using OdataToEntity.ModelBuilder;
using System;

namespace OdataToEntity
{
    public static class OeAnnotationExtensions
    {
        public static Type GetClrType(this IEdmModel edmModel, IEdmType edmType)
        {
            if (edmType.TypeKind == EdmTypeKind.Primitive)
                return PrimitiveTypeHelper.GetClrType((edmType as IEdmPrimitiveType).PrimitiveKind);

            OeValueAnnotation<Type> clrTypeAnnotation = edmModel.GetAnnotationValue<OeValueAnnotation<Type>>(edmType);
            if (clrTypeAnnotation != null)
                return clrTypeAnnotation.Value;

            if (edmType is IEdmCollectionType collectionType)
                return edmModel.GetClrType(collectionType.ElementType.Definition);

            throw new InvalidOperationException("Add type annotation for " + edmType.FullTypeName());
        }
        public static Db.OeDataAdapter GetDataAdapter(this IEdmModel edmModel, EdmEntityContainer entityContainer)
        {
            return edmModel.GetAnnotationValue<Db.OeDataAdapter>(entityContainer);
        }
        public static Db.OeEntitySetAdapter GetEntitySetAdapter(this IEdmModel edmModel, IEdmEntitySet entitySet)
        {
            return edmModel.GetAnnotationValue<Db.OeEntitySetAdapter>(entitySet);
        }
        public static ManyToManyJoinDescription GetManyToManyJoinDescription(this IEdmModel edmModel, IEdmNavigationProperty navigationProperty)
        {
            ManyToManyJoinDescription joinDescription = edmModel.GetAnnotationValue<ManyToManyJoinDescription>(navigationProperty);
            if (joinDescription != null)
                return joinDescription;

            throw new InvalidOperationException("Add many-to-many annotation for navigation property " + navigationProperty.Name);
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
        public static void SetDataAdapter(this IEdmModel edmModel, EdmEntityContainer entityContainer, Db.OeDataAdapter dataAdapter)
        {
            edmModel.SetAnnotationValue(entityContainer, dataAdapter);
        }
        public static void SetEntitySetAdapter(this IEdmModel edmModel, IEdmEntitySet entitySet, Db.OeEntitySetAdapter entitySetAdapter)
        {
            edmModel.SetAnnotationValue(entitySet, entitySetAdapter);
        }
        public static void SetIsDbFunction(this IEdmModel edmModel, IEdmOperation edmOperation, bool isDbFunction)
        {
            edmModel.SetAnnotationValue(edmOperation, new OeValueAnnotation<bool>(isDbFunction));
        }
        internal static void SetManyToManyJoinDescription(this IEdmModel edmModel, IEdmNavigationProperty navigationProperty, ManyToManyJoinDescription joinDescription)
        {
            edmModel.SetAnnotationValue(navigationProperty, joinDescription);
        }
    }
}
