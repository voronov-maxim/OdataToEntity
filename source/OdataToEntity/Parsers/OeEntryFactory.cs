using Microsoft.OData;
using Microsoft.OData.Edm;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace OdataToEntity.Parsers
{
    public sealed class OeEntryFactory
    {
        private readonly String _typeName;

        private OeEntryFactory(IEdmEntitySet entitySet, OePropertyAccessor[] accessors)
        {
            EntitySet = entitySet;
            Accessors = GetAccessorsWithoutSkiptoken(accessors);

            EntityType = entitySet.EntityType();
            NavigationLinks = Array.Empty<OeEntryFactory>();
            _typeName = EntityType.FullName();
        }
        private OeEntryFactory(IEdmEntitySet entitySet, OePropertyAccessor[] accessors,
            IReadOnlyList<OeEntryFactory> navigationLinks, Func<Object, Object> linkAccessor)
            : this(entitySet, accessors)
        {
            NavigationLinks = navigationLinks ?? Array.Empty<OeEntryFactory>();
            LinkAccessor = linkAccessor;
            EqualityComparer = new Infrastructure.OeEntryEqualityComparer(GetKeyExpressions(entitySet, accessors));
        }
        private OeEntryFactory(IEdmEntitySet entitySet, OePropertyAccessor[] accessors, ODataNestedResourceInfo resourceInfo)
            : this(entitySet, accessors)
        {
            ResourceInfo = resourceInfo;
        }
        private OeEntryFactory(IEdmEntitySet entitySet, OePropertyAccessor[] accessors, ODataNestedResourceInfo resourceInfo,
            IReadOnlyList<OeEntryFactory> navigationLinks, Func<Object, Object> linkAccessor)
            : this(entitySet, accessors)
        {
            ResourceInfo = resourceInfo;
            NavigationLinks = navigationLinks ?? Array.Empty<OeEntryFactory>();
            LinkAccessor = linkAccessor;
            EqualityComparer = new Infrastructure.OeEntryEqualityComparer(GetKeyExpressions(entitySet, accessors));
        }

        public ODataResource CreateEntry(Object entity)
        {
            var odataProperties = new ODataProperty[Accessors.Length];
            for (int i = 0; i < Accessors.Length; i++)
            {
                Object value = Accessors[i].GetValue(entity);
                ODataValue odataValue = OeEdmClrHelper.CreateODataValue(value);
                odataValue.TypeAnnotation = Accessors[i].TypeAnnotation;
                odataProperties[i] = new ODataProperty() { Name = Accessors[i].EdmProperty.Name, Value = odataValue };
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
        public static OeEntryFactory CreateEntryFactoryParent(IEdmEntitySet entitySet, OePropertyAccessor[] accessors,
            IReadOnlyList<OeEntryFactory> navigationLinks, Func<Object, Object> linkAccessor)
        {
            return new OeEntryFactory(entitySet, accessors, navigationLinks, linkAccessor);
        }
        public static OeEntryFactory CreateEntryFactoryNested(IEdmEntitySet entitySet, OePropertyAccessor[] accessors, ODataNestedResourceInfo resourceInfo,
            IReadOnlyList<OeEntryFactory> navigationLinks, Func<Object, Object> linkAccessor)
        {
            return new OeEntryFactory(entitySet, accessors, resourceInfo, navigationLinks, linkAccessor);
        }
        private static OePropertyAccessor[] GetAccessorsWithoutSkiptoken(OePropertyAccessor[] accessors)
        {
            int skiptokenCount = 0;
            for (int i = 0; i < accessors.Length; i++)
                if (accessors[i].SkipToken)
                    skiptokenCount++;

            if (skiptokenCount == 0)
                return accessors;

            var accessorsWithoutSkiptoken = new OePropertyAccessor[accessors.Length - skiptokenCount];
            int index = 0;
            for (int i = 0; i < accessors.Length; i++)
                if (!accessors[i].SkipToken)
                    accessorsWithoutSkiptoken[index++] = accessors[i];
            return accessorsWithoutSkiptoken;
        }
        private static List<MemberExpression> GetKeyExpressions(IEdmEntitySet entitySet, OePropertyAccessor[] accessors)
        {
            var propertyExpressions = new List<MemberExpression>();
            foreach (IEdmStructuralProperty key in entitySet.EntityType().Key())
            {
                bool found = false;
                foreach (OePropertyAccessor accessor in accessors)
                    if (accessor.EdmProperty == key)
                    {
                        propertyExpressions.Add(accessor.PropertyExpression);
                        found = true;
                        break;
                    }

                if (!found)
                    throw new InvalidOperationException("Key property " + key.Name + " not found in accessors");
            }
            return propertyExpressions;
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
        public IEqualityComparer<Object> EqualityComparer { get; }
        public Func<Object, Object> LinkAccessor { get; }
        public IReadOnlyList<OeEntryFactory> NavigationLinks { get; }
        public ODataNestedResourceInfo ResourceInfo { get; }
    }
}
