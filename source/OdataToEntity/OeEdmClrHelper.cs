using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using OdataToEntity.ModelBuilder;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace OdataToEntity
{
    public static class OeEdmClrHelper
    {
        public static Object CreateEntity(Type clrType, ODataResourceBase entry)
        {
            Object entity = Activator.CreateInstance(clrType)!;
            foreach (ODataProperty property in entry.Properties)
            {
                PropertyInfo? clrProperty = clrType.GetProperty(property.Name);
                if (clrProperty != null)
                {
                    Object value = GetClrValue(clrProperty.PropertyType, property.Value);
                    clrProperty.SetValue(entity, value);
                }
            }
            return entity;
        }
        public static ODataValue CreateODataValue(Object? value)
        {
            if (value == null)
                return new ODataNullValue();

            if (value.GetType().IsEnum)
                return new ODataEnumValue(value.ToString());

            if (value is DateTime dateTime)
            {
                DateTimeOffset dateTimeOffset = dateTime.Kind switch
                {
                    DateTimeKind.Unspecified => new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)),
                    DateTimeKind.Utc => new DateTimeOffset(dateTime),
                    DateTimeKind.Local => new DateTimeOffset(dateTime.ToUniversalTime()),
                    _ => throw new ArgumentOutOfRangeException("Unknown DateTimeKind " + dateTime.Kind.ToString()),
                };
                return new ODataPrimitiveValue(dateTimeOffset);
            }

            return new ODataPrimitiveValue(value);
        }
        public static ResourceRangeVariableReferenceNode CreateRangeVariableReferenceNode(IEdmEntitySetBase entitySet, String? name = null)
        {
            var entityTypeRef = (IEdmEntityTypeReference)((IEdmCollectionType)entitySet.Type).ElementType;
            var rangeVariable = new ResourceRangeVariable(name ?? "$it", entityTypeRef, entitySet);
            return new ResourceRangeVariableReferenceNode(name ?? "$it", rangeVariable);
        }
        public static Object GetClrValue(Type clrType, Object edmValue)
        {
            if (edmValue is ODataEnumValue enumValue)
            {
                Type? underlyingType = Nullable.GetUnderlyingType(clrType);
                return underlyingType == null ? Enum.Parse(clrType, enumValue.Value) : Enum.Parse(underlyingType, enumValue.Value);
            }

            if (edmValue is DateTimeOffset dateTimeOffset && (clrType == typeof(DateTime) || clrType == typeof(DateTime?)))
                return dateTimeOffset.UtcDateTime;

            return edmValue;
        }
        public static IEdmModel GetEdmModel(this IEdmModel edmModel, IEdmEntityContainer entityContainer)
        {
            if (edmModel.EntityContainer == entityContainer)
                return edmModel;

            foreach (IEdmModel refModel in edmModel.ReferencedModels)
                if (refModel.EntityContainer == entityContainer)
                    return refModel;

            throw new InvalidOperationException("EdmModel not found for EdmEntityContainer " + entityContainer.Name);
        }
        public static IEdmModel GetEdmModel(this IEdmModel edmModel, IEdmEntitySet entitySet)
        {
            return edmModel.GetEdmModel(entitySet.Container);
        }
        public static IEdmModel? GetEdmModel(this IEdmModel edmModel, IEdmEntityType edmEntityType)
        {
            foreach (IEdmSchemaElement element in edmModel.SchemaElements)
                if (element == edmEntityType)
                    return edmModel;

            foreach (IEdmModel refModel in edmModel.ReferencedModels)
                if (refModel is EdmModel)
                {
                    IEdmModel? foundModel = GetEdmModel(refModel, edmEntityType);
                    if (foundModel != null)
                        return foundModel;
                }

            return null;
        }
        public static IEdmModel? GetEdmModel(this IEdmModel edmModel, Type clrEntityType)
        {
            foreach (IEdmSchemaElement element in edmModel.SchemaElements)
                if (element is IEdmEntityType edmEntityType && edmModel.GetClrType(edmEntityType) == clrEntityType)
                    return edmModel;

            foreach (IEdmModel refModel in edmModel.ReferencedModels)
                if (refModel is EdmModel)
                {
                    IEdmModel? foundModel = GetEdmModel(refModel, clrEntityType);
                    if (foundModel != null)
                        return foundModel;
                }

            return null;
        }
        public static IEdmModel GetEdmModel(this IEdmModel edmModel, ODataPath path)
        {
            if (path.FirstSegment is EntitySetSegment entitySetSegment)
                return edmModel.GetEdmModel(entitySetSegment.EntitySet);

            if (path.FirstSegment is OperationImportSegment importSegment)
            {
                IEdmOperationImport operationImport = importSegment.OperationImports.Single();
                return edmModel.GetEdmModel(operationImport.Container);
            }

            if (path.FirstSegment == null)
                throw new InvalidOperationException("Invalid path first sgment is null");

            throw new InvalidOperationException("Not supported segment type " + path.FirstSegment.GetType().FullName);
        }
        public static IEdmTypeReference GetEdmTypeReference(Type clrType)
        {
            bool nullable;
            Type? underlyingType = Nullable.GetUnderlyingType(clrType);
            if (underlyingType == null)
                nullable = clrType.IsClass;
            else
            {
                clrType = underlyingType;
                nullable = true;
            }

            if (clrType.IsEnum)
                clrType = Enum.GetUnderlyingType(clrType);

            IEdmPrimitiveType? primitiveEdmType = PrimitiveTypeHelper.GetPrimitiveType(clrType);
            if (primitiveEdmType == null)
                throw new InvalidOperationException("Not found Edm type for Clr type " + clrType.FullName);

            return EdmCoreModel.Instance.GetPrimitive(primitiveEdmType.PrimitiveKind, nullable);
        }
        public static IEdmTypeReference GetEdmTypeReference(IEdmModel edmModel, Type clrType)
        {
            bool nullable;
            Type? underlyingType = Nullable.GetUnderlyingType(clrType);
            if (underlyingType == null)
                nullable = clrType.IsClass;
            else
            {
                clrType = underlyingType;
                nullable = true;
            }

            IEdmPrimitiveType? primitiveEdmType = PrimitiveTypeHelper.GetPrimitiveType(clrType);
            if (primitiveEdmType == null)
            {
                var edmEnumType = (IEdmEnumType)edmModel.FindType(clrType.FullName);
                return new EdmEnumTypeReference(edmEnumType, nullable);
            }

            return EdmCoreModel.Instance.GetPrimitive(primitiveEdmType.PrimitiveKind, nullable);
        }
        public static IEdmEntitySet GetEntitySet(IEdmModel edmModel, IEdmNavigationProperty navigationProperty)
        {
            foreach (IEdmEntitySet entitySet in edmModel.EntityContainer.EntitySets())
                foreach (IEdmNavigationPropertyBinding binding in entitySet.NavigationPropertyBindings)
                    if (binding.NavigationProperty == navigationProperty)
                        return (IEdmEntitySet)binding.Target;

            foreach (IEdmModel refModel in edmModel.ReferencedModels)
                if (refModel.EntityContainer != null && refModel is EdmModel)
                {
                    IEdmEntitySet entitySet = GetEntitySet(refModel, navigationProperty);
                    if (entitySet != null)
                        return entitySet;
                }

            throw new InvalidOperationException("EntitySet for navigation property " + navigationProperty.Name + " not found");
        }
        public static IEdmEntitySet GetEntitySet(IEdmModel edmModel, String entitySetName, StringComparison comparison = StringComparison.OrdinalIgnoreCase)
        {
            IEdmEntitySet? entitySet = GetEntitySetOrNull(edmModel, entitySetName, comparison);
            if (entitySet == null)
                throw new InvalidOperationException();
            return entitySet;
        }
        public static IEdmEntitySetBase GetEntitySet(IEdmModel edmModel, ExpandedNavigationSelectItem item)
        {
            var entitySet = (IEdmEntitySetBase)item.NavigationSource;
            if (entitySet == null)
            {
                var segment = (NavigationPropertySegment)item.PathToNavigationProperty.LastSegment;
                entitySet = OeEdmClrHelper.GetEntitySet(edmModel, segment.NavigationProperty);
            }
            return entitySet;
        }
        public static IEdmEntitySet GetEntitySet(ODataPath odataPath)
        {
            IReadOnlyList<Parsers.OeParseNavigationSegment> parseNavigationSegments = Parsers.OeParseNavigationSegment.GetNavigationSegments(odataPath);
            IEdmEntitySet? entitySet = Parsers.OeParseNavigationSegment.GetEntitySet(parseNavigationSegments);
            if (entitySet == null)
                if (odataPath.FirstSegment is EntitySetSegment entitySetSegment)
                    entitySet = entitySetSegment.EntitySet;
                else
                    throw new InvalidOperationException("Cannot get EntitySet from ODataPath");
            return entitySet;
        }
        public static IEdmEntitySet? GetEntitySetOrNull(IEdmModel edmModel, String entitySetName, StringComparison comparison = StringComparison.OrdinalIgnoreCase)
        {
            foreach (IEdmEntityContainerElement element in edmModel.EntityContainer.Elements)
                if (element is IEdmEntitySet edmEntitySet &&
                    String.Compare(edmEntitySet.Name, entitySetName, comparison) == 0)
                    return edmEntitySet;

            foreach (IEdmModel refModel in edmModel.ReferencedModels)
                if (refModel.EntityContainer != null && refModel is EdmModel)
                {
                    IEdmEntitySet? entitySet = GetEntitySetOrNull(refModel, entitySetName);
                    if (entitySet != null)
                        return entitySet;
                }

            return null;
        }
        public static int GetPageSize(this ExpandedNavigationSelectItem navigationSelectItem)
        {
            return navigationSelectItem.SelectAndExpand.GetPageSize();
        }
        public static int GetPageSize(this ODataUri odataUri)
        {
            return odataUri.SelectAndExpand.GetPageSize();
        }
        private static int GetPageSize(this SelectExpandClause selectExpandClause)
        {
            if (selectExpandClause != null)
                foreach (SelectItem selectItem in selectExpandClause.SelectedItems)
                    if (selectItem is Parsers.Translators.OePageSelectItem pageSelectItem)
                        return pageSelectItem.PageSize;

            return 0;
        }
        public static IEdmProperty GetPropertyIgnoreCase(this IEdmStructuredType entityType, String propertyName)
        {
            IEdmProperty? edmProperty = entityType.GetPropertyIgnoreCaseOrNull(propertyName);
            if (edmProperty == null)
                throw new InvalidOperationException("Property " + propertyName + " not found in IEdmStructuredType " + entityType.FullTypeName());

            return edmProperty;
        }
        public static PropertyInfo GetPropertyIgnoreCase(this Type declaringType, String propertyName)
        {
            PropertyInfo? propertyInfo = declaringType.GetPropertyIgnoreCaseOrNull(propertyName);
            if (propertyInfo == null)
                throw new InvalidOperationException("Property " + propertyName + " not found in Clr type " + declaringType.FullName);

            return propertyInfo;
        }
        public static PropertyInfo GetPropertyIgnoreCase(this Type declaringType, IEdmProperty edmProperty)
        {
            return declaringType.GetPropertyIgnoreCaseOrNull(edmProperty) ?? throw new InvalidOperationException("EdmProperty " + edmProperty.Name + " not found in type " + declaringType.FullName);
        }
        public static IEdmProperty? GetPropertyIgnoreCaseOrNull(this IEdmStructuredType entityType, String propertyName)
        {
            foreach (IEdmProperty edmProperty in entityType.Properties())
                if (String.Compare(edmProperty.Name, propertyName, StringComparison.OrdinalIgnoreCase) == 0)
                    return edmProperty;

            return null;
        }
        public static PropertyInfo? GetPropertyIgnoreCaseOrNull(this Type declaringType, String propertyName)
        {
            return declaringType.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        }
        public static PropertyInfo? GetPropertyIgnoreCaseOrNull(this Type declaringType, IEdmProperty edmProperty)
        {
            var schemaElement = (IEdmSchemaElement)edmProperty.DeclaringType;
            for (; ; )
            {
                if (String.Compare(declaringType.Name, schemaElement.Name, StringComparison.OrdinalIgnoreCase) == 0 &&
                    String.Compare(declaringType.Namespace, schemaElement.Namespace, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    PropertyInfo? propertyInfo = declaringType.GetPropertyIgnoreCaseOrNull(edmProperty.Name);
                    if (propertyInfo == null)
                    {
                        if (edmProperty is OeEdmStructuralShadowProperty structuralShadowProperty)
                            return Parsers.OeExpressionHelper.IsTupleType(declaringType) ? null : structuralShadowProperty.PropertyInfo;

                        if (edmProperty is OeEdmNavigationShadowProperty navigationShadowProperty)
                            return Parsers.OeExpressionHelper.IsTupleType(declaringType) ? null : navigationShadowProperty.PropertyInfo;
                    }

                    return propertyInfo;
                }

                if (declaringType.BaseType == null)
                    return null;

                declaringType = declaringType.BaseType;
            }
        }
        public static Object GetValue(IEdmModel edmModel, ODataEnumValue odataValue)
        {
            IEdmSchemaType schemaType = edmModel.FindType(odataValue.TypeName);
            Type clrType = edmModel.GetClrType((IEdmEnumType)schemaType.AsElementType());
            return Enum.Parse(clrType, odataValue.Value);
        }
        public static Object GetValue(IEdmModel edmModel, ODataCollectionValue odataValue)
        {
            IList? list = null;
            int nullCount = 0;
            foreach (Object odataItem in odataValue.Items)
            {
                Object? listItem = GetValue(edmModel, odataItem);
                if (list == null)
                {
                    if (listItem == null)
                        nullCount++;
                    else
                    {
                        Type listType = typeof(List<>).MakeGenericType(new[] { listItem.GetType() });
                        list = (IList)Activator.CreateInstance(listType)!;

                        while (nullCount > 0)
                        {
                            list.Add(null);
                            nullCount--;
                        }
                        list.Add(listItem);
                    }
                }
                else
                    list.Add(listItem);
            }

            return list ?? (new Object[nullCount]);
        }
        public static Object GetValue(IEdmModel edmModel, ODataResource odataValue)
        {
            Type clrType = edmModel.GetClrType(edmModel.FindType(odataValue.TypeName));
            Object instance = Activator.CreateInstance(clrType)!;
            foreach (ODataProperty edmProperty in odataValue.Properties)
            {
                PropertyInfo clrProperty = clrType.GetPropertyIgnoreCase(edmProperty.Name);
                if (clrProperty.PropertyType == typeof(DateTime) || clrProperty.PropertyType == typeof(DateTime?))
                    clrProperty.SetValue(instance, ((DateTimeOffset)edmProperty.Value).UtcDateTime);
                else
                    clrProperty.SetValue(instance, GetValue(edmModel, edmProperty.Value));
            }
            return instance;
        }
        public static Object? GetValue(IEdmModel edmModel, Object odataValue)
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
        public static bool IsNextLink(this SelectExpandClause selectExpandClause)
        {
            if (selectExpandClause != null)
                foreach (SelectItem selectItem in selectExpandClause.SelectedItems)
                    if (selectItem is Parsers.Translators.OeNextLinkSelectItem nextLinkSelectItem)
                        return nextLinkSelectItem.NextLink;

            return false;
        }
    }
}
