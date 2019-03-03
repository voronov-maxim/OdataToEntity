using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using OdataToEntity.ModelBuilder;
using System;
using System.Reflection;

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
        public static Type GetClrType(this IEdmModel edmModel, IEdmEntitySetBase entitySet)
        {
            IEdmEntityType entityType = entitySet.EntityType();
            edmModel = OeEdmClrHelper.GetEdmModel(edmModel, entityType);
            OeValueAnnotation<Type> clrTypeAnnotation = edmModel.GetAnnotationValue<OeValueAnnotation<Type>>(entityType);
            return clrTypeAnnotation.Value;
        }
        public static Db.OeDataAdapter GetDataAdapter(this IEdmModel edmModel, Type dataContextType)
        {
            Db.OeDataAdapter dataAdapter = edmModel.GetAnnotationValue<Db.OeDataAdapter>(edmModel.EntityContainer);
            if (dataAdapter.DataContextType == dataContextType)
                return dataAdapter;

            foreach (IEdmModel refModel in edmModel.ReferencedModels)
                if (refModel.EntityContainer != null)
                    return refModel.GetDataAdapter(dataContextType);

            throw new InvalidOperationException("OeDataAdapter not found for data context type " + dataContextType.FullName);
        }
        public static Db.OeDataAdapter GetDataAdapter(this IEdmModel edmModel, IEdmEntityContainer entityContainer)
        {
            Db.OeDataAdapter dataAdapter = edmModel.GetAnnotationValue<Db.OeDataAdapter>(entityContainer);
            if (dataAdapter == null)
                dataAdapter = edmModel.GetEdmModel(entityContainer).GetAnnotationValue<Db.OeDataAdapter>(entityContainer);

            return dataAdapter;
        }
        public static Db.OeEntitySetAdapter GetEntitySetAdapter(this IEdmModel edmModel, IEdmEntitySet entitySet)
        {
            Db.OeEntitySetAdapter entitySetAdapter = edmModel.GetAnnotationValue<Db.OeEntitySetAdapter>(entitySet);
            if (entitySetAdapter == null)
                entitySetAdapter = edmModel.GetEdmModel(entitySet).GetAnnotationValue<Db.OeEntitySetAdapter>(entitySet);

            return entitySetAdapter;
        }
        public static ManyToManyJoinDescription GetManyToManyJoinDescription(this IEdmModel edmModel, IEdmNavigationProperty navigationProperty)
        {
            ManyToManyJoinDescription joinDescription = edmModel.GetAnnotationValue<ManyToManyJoinDescription>(navigationProperty);
            if (joinDescription != null)
                return joinDescription;

            throw new InvalidOperationException("Add many-to-many annotation for navigation property " + navigationProperty.Name);
        }
        internal static MethodInfo GetMethodInfo(this IEdmModel edmModel, IEdmFunction edmFunction)
        {
            return edmModel.GetAnnotationValue<MethodInfo>(edmFunction);
        }
        public static T GetModelBoundAttribute<T>(this IEdmModel edmModel, IEdmEntityType entityType) where T : Attribute
        {
            return edmModel.GetEdmModel(entityType).GetAnnotationValue<T>(entityType);
        }
        public static T GetModelBoundAttribute<T>(this IEdmModel edmModel, IEdmNavigationProperty navigationProperty) where T : Attribute
        {
            IEdmEntitySet entitySet = OeEdmClrHelper.GetEntitySet(edmModel, navigationProperty);
            return OeEdmClrHelper.GetEdmModel(edmModel, entitySet.Container).GetAnnotationValue<T>(navigationProperty);
        }
        public static SelectItem[] GetSelectItems(this IEdmModel edmModel, IEdmEntityType entityType)
        {
            return edmModel.GetEdmModel(entityType).GetAnnotationValue<SelectItem[]>(entityType);
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
        internal static void SetMethodInfo(this IEdmModel edmModel, IEdmFunction edmFunction, MethodInfo methodInfo)
        {
            edmModel.SetAnnotationValue(edmFunction, methodInfo);
        }
        public static void SetModelBoundAttribute<T>(this IEdmModel edmModel, IEdmEntityType entityType, T attribute) where T : Attribute
        {
            OeEdmClrHelper.GetEdmModel(edmModel, entityType).SetAnnotationValue(entityType, attribute);
        }
        public static void SetModelBoundAttribute<T>(this IEdmModel edmModel, IEdmNavigationProperty navigationProperty, T attribute) where T : Attribute
        {
            IEdmEntitySet entitySet = OeEdmClrHelper.GetEntitySet(edmModel, navigationProperty);
            OeEdmClrHelper.GetEdmModel(edmModel, entitySet.Container).SetAnnotationValue(navigationProperty, attribute);
        }
        public static void SetSelectItems(this IEdmModel edmModel, IEdmEntityType entityType, SelectItem[] selectItems)
        {
            OeEdmClrHelper.GetEdmModel(edmModel, entityType).SetAnnotationValue(entityType, selectItems);
        }
    }
}
