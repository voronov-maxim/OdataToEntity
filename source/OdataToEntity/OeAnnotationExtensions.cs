using Microsoft.OData.Edm;
using OdataToEntity.ModelBuilder;
using System;
using System.Reflection;

namespace OdataToEntity
{
    public static class OeAnnotationExtensions
    {
        public static Type GetClrType(this IEdmModel edmModel, IEdmType edmType)
        {
            Type? clrType = GetClrTypeImpl(edmModel, edmType);
            if (clrType == null)
                throw new InvalidOperationException("Add type annotation for " + edmType.FullTypeName());

            return clrType;
        }
        private static Type? GetClrTypeImpl(IEdmModel edmModel, IEdmType edmType)
        {
            if (edmType is IEdmPrimitiveType edmPrimitiveType)
                return PrimitiveTypeHelper.GetClrType(edmPrimitiveType.PrimitiveKind);

            Type? clrType = edmModel.GetAnnotationValue<Type>(edmType);
            if (clrType != null)
                return clrType;

            if (edmType is IEdmCollectionType collectionType)
                return GetClrTypeImpl(edmModel, collectionType.ElementType.Definition);

            foreach (IEdmModel refModel in edmModel.ReferencedModels)
                if (refModel is EdmModel)
                {
                    clrType = GetClrTypeImpl(refModel, edmType);
                    if (clrType != null)
                        return clrType;
                }

            return null;
        }
        public static Type GetClrType(this IEdmModel edmModel, IEdmEntitySetBase entitySet)
        {
            IEdmEntityType entityType = entitySet.EntityType();
            IEdmModel? model = OeEdmClrHelper.GetEdmModel(edmModel, entityType);
            if (model == null)
                throw new InvalidOperationException("Add type annotation for " + entityType.FullTypeName());
            return model.GetAnnotationValue<Type>(entityType);
        }
        public static Db.OeDataAdapter GetDataAdapter(this IEdmModel edmModel, Type dataContextType)
        {
            Db.OeDataAdapter dataAdapter = edmModel.GetAnnotationValue<Db.OeDataAdapter>(edmModel.EntityContainer);
            if (dataAdapter.DataContextType == dataContextType)
                return dataAdapter;

            foreach (IEdmModel refModel in edmModel.ReferencedModels)
                if (refModel.EntityContainer != null && refModel is EdmModel)
                    return refModel.GetDataAdapter(dataContextType);

            throw new InvalidOperationException("OeDataAdapter not found for data context type " + dataContextType.FullName);
        }
        public static Db.OeDataAdapter GetDataAdapter(this IEdmModel edmModel, IEdmEntityContainer entityContainer)
        {
            Db.OeDataAdapter dataAdapter = edmModel.GetAnnotationValue<Db.OeDataAdapter>(entityContainer);
            if (dataAdapter == null)
                dataAdapter = edmModel.GetEdmModel(entityContainer).GetAnnotationValue<Db.OeDataAdapter>(entityContainer);

            if (dataAdapter == null)
                throw new InvalidOperationException("OeDataAdapter not found in EdmModel");

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
        public static bool IsDbFunction(this IEdmModel edmModel, IEdmOperation edmOperation)
        {
            OeValueAnnotation<bool> valueAnnotation = edmModel.GetAnnotationValue<OeValueAnnotation<bool>>(edmOperation);
            return valueAnnotation.Value;
        }
        public static void SetClrType(this IEdmModel edmModel, IEdmType edmType, Type clrType)
        {
            edmModel.SetAnnotationValue(edmType, clrType);
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
    }
}
