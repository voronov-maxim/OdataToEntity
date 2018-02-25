using Microsoft.OData;
using Microsoft.OData.Edm;
using System;
using System.Collections;
using System.Collections.Generic;

namespace OdataToEntity.Parsers
{
    public sealed class OeEntryFactory
    {
        private readonly String _typeName;

        private OeEntryFactory(IEdmEntitySet entitySet, OePropertyAccessor[] accessors)
        {
            EntitySet = entitySet;
            Accessors = accessors;

            EntityType = entitySet.EntityType();
            NavigationLinks = Array.Empty<OeEntryFactory>();
            _typeName = EntityType.FullName();
        }
        private OeEntryFactory(IEdmEntitySet entitySet, OePropertyAccessor[] accessors, IReadOnlyList<OeEntryFactory> navigationLinks)
            : this(entitySet, accessors)
        {
            NavigationLinks = navigationLinks ?? Array.Empty<OeEntryFactory>();
        }
        private OeEntryFactory(IEdmEntitySet entitySet, OePropertyAccessor[] accessors, ODataNestedResourceInfo resourceInfo)
            : this(entitySet, accessors)
        {
            ResourceInfo = resourceInfo;
        }
        private OeEntryFactory(IEdmEntitySet entitySet, OePropertyAccessor[] accessors, ODataNestedResourceInfo resourceInfo, IReadOnlyList<OeEntryFactory> navigationLinks)
            : this(entitySet, accessors)
        {
            ResourceInfo = resourceInfo;
            NavigationLinks = navigationLinks;
        }

        public ODataResource CreateEntry(Object entity)
        {
            var odataProperties = new ODataProperty[Accessors.Length];
            for (int i = 0; i < Accessors.Length; i++)
            {
                OePropertyAccessor accessor = Accessors[i];
                Object value = accessor.Accessor(entity);

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

                odataValue.TypeAnnotation = accessor.TypeAnnotation;
                odataProperties[i] = new ODataProperty() { Name = accessor.Name, Value = odataValue };
            }

            return new ODataResource
            {
                TypeName = _typeName,
                Properties = odataProperties
            };
        }
        public static OeEntryFactory CreateEntryFactory(IEdmEntitySet entitySet, OePropertyAccessor[] accessors)
        {
            return new OeEntryFactory(entitySet, accessors);
        }
        public static OeEntryFactory CreateEntryFactoryChild(IEdmEntitySet entitySet, OePropertyAccessor[] accessors, ODataNestedResourceInfo resourceInfo)
        {
            return new OeEntryFactory(entitySet, accessors, resourceInfo);
        }
        public static OeEntryFactory CreateEntryFactoryParent(IEdmEntitySet entitySet, OePropertyAccessor[] accessors, IReadOnlyList<OeEntryFactory> navigationLinks)
        {
            return new OeEntryFactory(entitySet, accessors, navigationLinks);
        }
        public static OeEntryFactory CreateEntryFactoryNested(IEdmEntitySet entitySet, OePropertyAccessor[] accessors, ODataNestedResourceInfo resourceInfo, IReadOnlyList<OeEntryFactory> navigationLinks)
        {
            return new OeEntryFactory(entitySet, accessors, resourceInfo, navigationLinks);
        }
        public static OeEntryFactory CreateNextLink(IEdmEntitySet entitySet, ODataNestedResourceInfo resourceInfo)
        {
            return new OeEntryFactory(entitySet, Array.Empty<OePropertyAccessor>(), resourceInfo);
        }
        public Object GetValue(Object value, out int? count)
        {
            count = null;
            if (LinkAccessor == null)
                return value;

            value = LinkAccessor(value);
            if (CountOption.GetValueOrDefault())
            {
                if (value == null)
                    count = 0;
                else if (value is IReadOnlyCollection<Object>)
                    count = (value as IReadOnlyCollection<Object>).Count;
                else if (value is ICollection)
                    count = (value as ICollection).Count;
                else if (value is IEnumerable)
                {
                    var list = new List<Object>();
                    foreach (Object item in value as IEnumerable)
                        list.Add(item);
                    count = list.Count;
                    value = list;
                }
                else
                    count = 1;
            }
            return value;
        }

        public OePropertyAccessor[] Accessors { get; }
        public bool? CountOption { get; set; }
        public IEdmEntitySet EntitySet { get; }
        public IEdmEntityType EntityType { get; }
        public Func<Object, Object> LinkAccessor { get; set; }
        public IReadOnlyList<OeEntryFactory> NavigationLinks { get; }
        public ODataNestedResourceInfo ResourceInfo { get; }
    }
}
