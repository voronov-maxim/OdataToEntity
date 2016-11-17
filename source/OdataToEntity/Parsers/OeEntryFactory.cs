using Microsoft.OData;
using Microsoft.OData.Edm;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace OdataToEntity.Parsers
{
    public sealed class OeEntryFactory
    {
        private readonly OePropertyAccessor[] _accessors;
        private readonly IEdmEntitySetBase _entitySet;
        private readonly IEdmEntityType _entityType;
        private readonly ODataNestedResourceInfo _resourceInfo;
        private readonly IReadOnlyList<OeEntryFactory> _navigationLinks;
        private readonly ODataProperty[] _odataProperties;
        private readonly String _typeName;

        private OeEntryFactory(IEdmEntitySetBase entitySet, OePropertyAccessor[] accessors)
        {
            _entitySet = entitySet;
            _accessors = accessors;

            _entityType = entitySet.EntityType();
            _navigationLinks = Array.Empty<OeEntryFactory>();
            _typeName = EntityType.FullName();

            _odataProperties = new ODataProperty[accessors.Length];
            for (int i = 0; i < accessors.Length; i++)
                _odataProperties[i] = new ODataProperty() { Name = accessors[i].Name };
        }
        private OeEntryFactory(IEdmEntitySetBase entitySet, OePropertyAccessor[] accessors, IReadOnlyList<OeEntryFactory> navigationLinks)
            : this(entitySet, accessors)
        {
            _navigationLinks = navigationLinks ?? Array.Empty<OeEntryFactory>();
        }
        private OeEntryFactory(IEdmEntitySetBase entitySet, OePropertyAccessor[] accessors, ODataNestedResourceInfo resourceInfo)
            : this(entitySet, accessors)
        {
            _resourceInfo = resourceInfo;
        }
        private OeEntryFactory(IEdmEntitySetBase entitySet, OePropertyAccessor[] accessors, ODataNestedResourceInfo resourceInfo, IReadOnlyList<OeEntryFactory> navigationLinks)
            : this(entitySet, accessors)
        {
            _resourceInfo = resourceInfo;
            _navigationLinks = navigationLinks;
        }

        public ODataResource CreateEntry(Object entity)
        {
            for (int i = 0; i < _accessors.Length; i++)
            {
                OePropertyAccessor accessor = _accessors[i];
                Object value = accessor.Accessor(entity);
                if (value is DateTime)
                    value = (DateTimeOffset)(DateTime)value;
                ODataValue odataValue = null;
                if (value == null)
                    odataValue = new ODataNullValue() { TypeAnnotation = accessor.TypeAnnotation };
                else
                {
                    if (value.GetType().GetTypeInfo().IsEnum)
                        odataValue = new ODataEnumValue(value.ToString());
                    else
                        odataValue = new ODataPrimitiveValue(value);
                    odataValue.TypeAnnotation = accessor.TypeAnnotation;
                }
                _odataProperties[i].Value = odataValue;
            }

            return new ODataResource
            {
                TypeName = _typeName,
                Properties = _odataProperties
            };
        }
        public static OeEntryFactory CreateEntryFactory(IEdmEntitySetBase entitySet, OePropertyAccessor[] accessors)
        {
            return new OeEntryFactory(entitySet, accessors);
        }
        public static OeEntryFactory CreateEntryFactoryChild(IEdmEntitySetBase entitySet, OePropertyAccessor[] accessors, ODataNestedResourceInfo resourceInfo)
        {
            return new OeEntryFactory(entitySet, accessors, resourceInfo);
        }
        public static OeEntryFactory CreateEntryFactoryParent(IEdmEntitySetBase entitySet, OePropertyAccessor[] accessors, IReadOnlyList<OeEntryFactory> navigationLinks)
        {
            return new OeEntryFactory(entitySet, accessors, navigationLinks);
        }
        public static OeEntryFactory CreateEntryFactoryNested(IEdmEntitySetBase entitySet, OePropertyAccessor[] accessors, ODataNestedResourceInfo resourceInfo, IReadOnlyList<OeEntryFactory> navigationLinks)
        {
            return new OeEntryFactory(entitySet, accessors, resourceInfo, navigationLinks);
        }
        public Object GetValue(Object value)
        {
            return LinkAccessor == null ? value : LinkAccessor(value);
        }

        public OePropertyAccessor[] Accessors => _accessors;
        public IEdmEntitySetBase EntitySet => _entitySet;
        public IEdmEntityType EntityType => _entityType;
        public Func<Object, Object> LinkAccessor { get; set; }
        public IReadOnlyList<OeEntryFactory> NavigationLinks => _navigationLinks;
        public ODataNestedResourceInfo ResourceInfo => _resourceInfo;
    }
}
