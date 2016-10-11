using Microsoft.OData;
using Microsoft.OData.Edm;
using System;
using System.Reflection;

namespace OdataToEntity.Parsers
{
    public sealed class OeEntryFactory
    {
        private readonly OePropertyAccessor[] _accessors;
        private readonly IEdmEntitySetBase _entitySet;
        private readonly IEdmEntityType _entityType;
        private readonly ODataResource _entryTemplate;
        private readonly ODataNestedResourceInfo _link;
        private readonly Func<Object, Object> _linkAccessor;
        private readonly OeEntryFactory[] _navigationLinks;

        private OeEntryFactory(IEdmEntitySetBase entitySet, OePropertyAccessor[] accessors)
        {
            _entitySet = entitySet;
            _accessors = accessors;

            _entityType = entitySet.EntityType();
            _navigationLinks = Array.Empty<OeEntryFactory>();
            _entryTemplate = CreateEntryTemplate(_entityType, _accessors);
        }
        private OeEntryFactory(IEdmEntitySetBase entitySet, OePropertyAccessor[] accessors, Func<Object, Object> linkAccessor, OeEntryFactory[] navigationLinks)
            : this(entitySet, accessors)
        {
            _navigationLinks = navigationLinks ?? Array.Empty<OeEntryFactory>();
            _linkAccessor = linkAccessor;
        }
        private OeEntryFactory(IEdmEntitySetBase entitySet, OePropertyAccessor[] accessors, Func<Object, Object> linkAccessor, ODataNestedResourceInfo link)
            : this(entitySet, accessors)
        {
            _link = link;
            _linkAccessor = linkAccessor;
        }

        public ODataResource CreateEntry(Object entity)
        {
            var odataProperties = (ODataProperty[])_entryTemplate.Properties;
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
                odataProperties[i].Value = odataValue;
            }

            return _entryTemplate;
        }
        public static OeEntryFactory CreateEntryFactory(IEdmEntitySetBase entitySet, OePropertyAccessor[] accessors)
        {
            return new OeEntryFactory(entitySet, accessors);
        }
        public static OeEntryFactory CreateEntryFactoryChild(IEdmEntitySetBase entitySet, OePropertyAccessor[] accessors,
            Func<Object, Object> accessor, ODataNestedResourceInfo link)
        {
            return new OeEntryFactory(entitySet, accessors, accessor, link);
        }
        public static OeEntryFactory CreateEntryFactoryParent(IEdmEntitySetBase entitySet, OePropertyAccessor[] accessors,
            Func<Object, Object> accessor, OeEntryFactory[] navigationLinks)
        {
            return new OeEntryFactory(entitySet, accessors, accessor, navigationLinks);
        }
        private static ODataResource CreateEntryTemplate(IEdmEntityType entityType, OePropertyAccessor[] accessors)
        {
            var odataProperties = new ODataProperty[accessors.Length];
            for (int i = 0; i < accessors.Length; i++)
                odataProperties[i] = new ODataProperty() { Name = accessors[i].Name };

            return new ODataResource
            {
                TypeName = entityType.FullName(),
                Properties = odataProperties
            };
        }
        public Object GetValue(Object value)
        {
            return _linkAccessor == null ? value : _linkAccessor(value);
        }

        public IEdmEntitySetBase EntitySet => _entitySet;
        public IEdmEntityType EntityType => _entityType;
        public ODataNestedResourceInfo Link => _link;
        public OeEntryFactory[] NavigationLinks => _navigationLinks;
    }
}
