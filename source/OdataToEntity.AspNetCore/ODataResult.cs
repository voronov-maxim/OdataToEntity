using Microsoft.AspNetCore.Mvc;
using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using OdataToEntity.Parsers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace OdataToEntity.AspNetCore
{
    public sealed class ODataResult<T> : ActionResult
    {
        private struct EntityPropertiesInfo
        {
            public readonly IEdmEntityType EdmEntityType;
            public readonly PropertyInfo[] Structurals;
            public readonly PropertyInfo[] Navigations;

            public EntityPropertiesInfo(IEdmEntityType edmEntityType, PropertyInfo[] structurals, PropertyInfo[] navigations)
            {
                EdmEntityType = edmEntityType;
                Structurals = structurals;
                Navigations = navigations;
            }
        }

        private sealed class SelectProperyHandler : SelectItemHandler
        {
            private readonly IEdmStructuredType _edmStructuredType;

            public SelectProperyHandler(IEdmStructuredType edmStructuredType)
            {
                _edmStructuredType = edmStructuredType;

                NavigationProperties = new List<IEdmNavigationProperty>();
                StructuralProperties = new List<IEdmStructuralProperty>();
            }
            public override void Handle(ExpandedNavigationSelectItem item)
            {
                var navigationSegment = (NavigationPropertySegment)item.PathToNavigationProperty.LastSegment;
                if (navigationSegment.NavigationProperty.DeclaringType == _edmStructuredType)
                    NavigationProperties.Add(navigationSegment.NavigationProperty);
                else
                    Handle(item.SelectAndExpand);
            }
            public override void Handle(PathSelectItem item)
            {
                if (item.SelectedPath.LastSegment is NavigationPropertySegment navigationSegment)
                {
                    if (navigationSegment.NavigationProperty.DeclaringType == _edmStructuredType &&
                        !NavigationProperties.Contains(navigationSegment.NavigationProperty))
                        NavigationProperties.Add(navigationSegment.NavigationProperty);
                }
                else if (item.SelectedPath.LastSegment is PropertySegment propertySegment)
                {
                    if (propertySegment.Property.DeclaringType == _edmStructuredType)
                        StructuralProperties.Add(propertySegment.Property);
                }
                else
                    throw new InvalidOperationException(item.SelectedPath.LastSegment.GetType().Name + " not supported");
            }
            public void Handle(SelectExpandClause selectAndExpand)
            {
                foreach (SelectItem selectItem in selectAndExpand.SelectedItems)
                    selectItem.HandleWith(this);
            }

            public List<IEdmNavigationProperty> NavigationProperties { get; }
            public List<IEdmStructuralProperty> StructuralProperties { get; }
        }

        private readonly IEdmModel _edmModel;
        private readonly IAsyncEnumerator<T> _entities;
        private OeMetadataLevel _metadataLevel;
        private readonly ODataUri _odataUri;
        private readonly HashSet<Object> _stack;

        public ODataResult(IEdmModel edmModel, ODataUri odataUri, IAsyncEnumerator<T> entities)
        {
            _odataUri = odataUri;
            _edmModel = edmModel;
            _entities = entities;

            _stack = new HashSet<Object>();
        }

        private Uri BuildNextPageLink(String skipToken)
        {
            ODataUri nextOdataUri = _odataUri.Clone();
            nextOdataUri.ServiceRoot = null;
            nextOdataUri.QueryCount = null;
            nextOdataUri.Top = PageSize;
            nextOdataUri.Skip = null;
            nextOdataUri.SkipToken = skipToken;

            return nextOdataUri.BuildUri(ODataUrlKeyDelimiter.Parentheses);
        }
        private static ODataResource CreateEntry(Object entity, PropertyInfo[] structuralProperties)
        {
            Type clrEntityType = entity.GetType();
            var odataProperties = new ODataProperty[structuralProperties.Length];
            for (int i = 0; i < odataProperties.Length; i++)
            {
                Object value = structuralProperties[i].GetValue(entity);
                ODataValue odataValue = null;
                if (value == null)
                    odataValue = new ODataNullValue();
                else if (value.GetType().IsEnum)
                    odataValue = new ODataEnumValue(value.ToString());
                else if (value is DateTime dateTime)
                {
                    switch (dateTime.Kind)
                    {
                        case DateTimeKind.Unspecified:
                            value = new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc));
                            break;
                        case DateTimeKind.Utc:
                            value = new DateTimeOffset(dateTime);
                            break;
                        case DateTimeKind.Local:
                            value = new DateTimeOffset(dateTime.ToUniversalTime());
                            break;
                        default:
                            throw new ArgumentOutOfRangeException("unknown DateTimeKind " + dateTime.Kind.ToString());
                    }
                    odataValue = new ODataPrimitiveValue(value);
                }
                else
                    odataValue = new ODataPrimitiveValue(value);

                odataProperties[i] = new ODataProperty() { Name = structuralProperties[i].Name, Value = odataValue };
            }

            return new ODataResource
            {
                TypeName = clrEntityType.FullName,
                Properties = odataProperties
            };
        }
        public override void ExecuteResult(ActionContext context)
        {
            throw new NotImplementedException();
        }
        public override async Task ExecuteResultAsync(ActionContext context)
        {
            var settings = new ODataMessageWriterSettings()
            {
                BaseUri = _odataUri.ServiceRoot,
                EnableMessageStreamDisposal = false,
                ODataUri = _odataUri,
                Validations = ValidationKinds.ThrowIfTypeConflictsWithMetadata | ValidationKinds.ThrowOnDuplicatePropertyNames,
                Version = ODataVersion.V4
            };

            var requestHeaders = OeRequestHeaders.Parse(context.HttpContext.Request.Headers["Accept"], context.HttpContext.Request.Headers["Prefer"]);
            _metadataLevel = requestHeaders.MetadataLevel;
            if (requestHeaders.MaxPageSize > 0 && PageSize == 0)
                PageSize = requestHeaders.MaxPageSize;

            IODataResponseMessage responseMessage = new OeInMemoryMessage(context.HttpContext.Response.Body, context.HttpContext.Request.ContentType);
            using (ODataMessageWriter messageWriter = new ODataMessageWriter(responseMessage, settings, _edmModel))
            {
                ODataUtils.SetHeadersForPayload(messageWriter, ODataPayloadKind.ResourceSet);

                IEdmEntitySet edmEntitySet = null;
                IEdmEntityType edmEntityType = null;
                foreach (IEdmEntitySet element in _edmModel.EntityContainer.EntitySets())
                {
                    edmEntityType = element.EntityType();
                    if (edmEntityType.FullName() == typeof(T).FullName)
                    {
                        edmEntitySet = element;
                        break;
                    }
                }

                ODataWriter writer = messageWriter.CreateODataResourceSetWriter(edmEntitySet, edmEntityType);
                await SerializeAsync(writer);
            }
        }
        private EntityPropertiesInfo GetProperties(Object entity)
        {
            Type clrEntityType = entity.GetType();
            var edmEntityType = (IEdmEntityType)_edmModel.FindDeclaredType(clrEntityType.FullName);

            IEnumerable<IEdmStructuralProperty> structuralProperties;
            IEnumerable<IEdmNavigationProperty> navigationProperties;
            if (_odataUri.SelectAndExpand == null)
            {
                structuralProperties = edmEntityType.StructuralProperties();
                navigationProperties = edmEntityType.NavigationProperties();
            }
            else
            {
                var handler = new SelectProperyHandler(edmEntityType);
                handler.Handle(_odataUri.SelectAndExpand);

                if (handler.StructuralProperties.Count == 0 && handler.NavigationProperties.Count == 0)
                {
                    structuralProperties = edmEntityType.StructuralProperties();
                    navigationProperties = edmEntityType.NavigationProperties();
                }
                else
                {
                    if (handler.StructuralProperties.Count == 0)
                        structuralProperties = edmEntityType.StructuralProperties();
                    else
                    {
                        if (_metadataLevel == OeMetadataLevel.Full)
                            foreach (IEdmStructuralProperty keyProperty in edmEntityType.Key())
                                if (!handler.StructuralProperties.Contains(keyProperty))
                                    handler.StructuralProperties.Add(keyProperty);

                        structuralProperties = handler.StructuralProperties;
                    }

                    navigationProperties = handler.NavigationProperties;
                }
            }

            return new EntityPropertiesInfo(edmEntityType, GetProperties(structuralProperties), GetProperties(navigationProperties));

            PropertyInfo[] GetProperties(IEnumerable<IEdmProperty> edmProperties)
            {
                var clrProperties = new List<PropertyInfo>();
                foreach (IEdmProperty edmProperty in edmProperties)
                    clrProperties.Add(clrEntityType.GetProperty(edmProperty.Name));
                return clrProperties.ToArray();
            }
        }
        private async Task SerializeAsync(ODataWriter writer)
        {
            var resourceSet = new ODataResourceSet() { Count = Count };
            writer.WriteStart(resourceSet);

            int count = 0;
            T entity = default(T);
            EntityPropertiesInfo entityPropertiesInfo = default(EntityPropertiesInfo);
            while (await _entities.MoveNext())
            {
                entity = _entities.Current;
                _stack.Add(entity);
                WriteEntry(writer, entity, ref entityPropertiesInfo);
                _stack.Remove(entity);
                count++;
            }

            if (PageSize > 0 && count > 0 && (Count ?? Int32.MaxValue) > count)
                resourceSet.NextPageLink = BuildNextPageLink(OeSkipTokenParser.GetSkipToken(_edmModel, GetKeys()));

            writer.WriteEnd();

            IEnumerable<KeyValuePair<String, Object>> GetKeys()
            {
                Type clrType = entity.GetType();
                foreach (IEdmStructuralProperty edmProperty in entityPropertiesInfo.EdmEntityType.Key())
                {
                    PropertyInfo clrProperty = clrType.GetProperty(edmProperty.Name);
                    yield return new KeyValuePair<String, Object>(clrProperty.Name, clrProperty.GetValue(entity));
                }
           }
        }
        private void WriteEntry(ODataWriter writer, Object entity, ref EntityPropertiesInfo entityPropertiesInfo)
        {
            if (entityPropertiesInfo.EdmEntityType == null)
                entityPropertiesInfo = GetProperties(entity);

            ODataResource entry = CreateEntry(entity, entityPropertiesInfo.Structurals);
            writer.WriteStart(entry);

            foreach (PropertyInfo navigationProperty in entityPropertiesInfo.Navigations)
                WriteNavigationProperty(writer, entity, navigationProperty);

            writer.WriteEnd();
        }
        private void WriteNavigationProperty(ODataWriter writer, Object value, PropertyInfo navigationProperty)
        {
            Object navigationValue = navigationProperty.GetValue(value);
            if (!_stack.Add(navigationValue))
                return;

            bool isCollection = OeExpressionHelper.GetCollectionItemType(navigationProperty.PropertyType) != null;
            var resourceInfo = new ODataNestedResourceInfo()
            {
                IsCollection = isCollection,
                Name = navigationProperty.Name
            };
            writer.WriteStart(resourceInfo);

            if (navigationValue == null)
            {
                if (isCollection)
                    writer.WriteStart(new ODataResourceSet());
                else
                    writer.WriteStart((ODataResource)null);
                writer.WriteEnd();
            }
            else
            {
                var entityPropertiesInfo = default(EntityPropertiesInfo);
                if (isCollection)
                {
                    writer.WriteStart(new ODataResourceSet());
                    foreach (Object entity in (IEnumerable)navigationValue)
                        WriteEntry(writer, entity, ref entityPropertiesInfo);
                    writer.WriteEnd();
                }
                else
                    WriteEntry(writer, navigationValue, ref entityPropertiesInfo);
            }

            writer.WriteEnd();
            _stack.Remove(navigationValue);
        }

        public int? Count { get; set; }
        public int PageSize { get; set; }
    }
}