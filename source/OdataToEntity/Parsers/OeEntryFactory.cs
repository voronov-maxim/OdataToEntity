using Microsoft.OData;
using Microsoft.OData.Edm;
using System;
using System.Collections;
using System.Collections.Generic;

namespace OdataToEntity.Parsers
{
    public sealed class OeEntryFactory
    {
        private sealed class EntryEqualityComparer : IEqualityComparer<Object>
        {
            private readonly OePropertyAccessor[] _keyAccessors;

            public EntryEqualityComparer(OePropertyAccessor[] keyAccessors)
            {
                _keyAccessors = keyAccessors;
            }

            public new bool Equals(Object x, Object y)
            {
                if (Object.ReferenceEquals(x, y))
                    return true;

                if (x == null || y == null)
                    return false;

                foreach (OePropertyAccessor keyAccessor in _keyAccessors)
                {
                    var xkey = (IComparable)keyAccessor.GetValue(x);
                    var ykey = (IComparable)keyAccessor.GetValue(y);

                    if (!Object.ReferenceEquals(xkey, ykey))
                    {
                        if (xkey == null || ykey == null)
                            return false;

                        if (xkey.CompareTo(ykey) != 0)
                            return false;
                    }
                }

                return true;
            }

            public int GetHashCode(Object obj)
            {
                int hashCode = 0;
                foreach (OePropertyAccessor keyAccessor in _keyAccessors)
                {
                    Object key = keyAccessor.GetValue(obj);
                    if (key != null)
                        hashCode = (hashCode << 5) + hashCode ^ key.GetHashCode();
                }
                return hashCode;
            }
        }

        private readonly String _typeName;

        private OeEntryFactory(IEdmEntitySet entitySet, OePropertyAccessor[] accessors)
        {
            EntitySet = entitySet;
            Accessors = accessors;

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
            EqualityComparer = CreateEqualityComparer(entitySet, accessors);
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
            EqualityComparer = CreateEqualityComparer(entitySet, accessors);
        }

        private static EntryEqualityComparer CreateEqualityComparer(IEdmEntitySet entitySet, OePropertyAccessor[] accessors)
        {
            var keyAccessors = new List<OePropertyAccessor>();
            foreach (IEdmStructuralProperty key in entitySet.EntityType().Key())
            {
                bool found = false;
                foreach (OePropertyAccessor accessor in accessors)
                    if (accessor.EdmProperty == key)
                    {
                        keyAccessors.Add(accessor);
                        found = true;
                        break;
                    }

                if (!found)
                    throw new InvalidOperationException("Key property " + key.Name + " not found in accessors");
            }

            return new EntryEqualityComparer(keyAccessors.ToArray());
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
