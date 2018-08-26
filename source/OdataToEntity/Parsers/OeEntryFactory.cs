using Microsoft.OData;
using Microsoft.OData.Edm;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace OdataToEntity.Parsers
{
    public sealed class OeEntryFactory
    {
        private sealed class AccessorByNameComparer : IComparer<OePropertyAccessor>
        {
            public static readonly AccessorByNameComparer Instance = new AccessorByNameComparer();

            private AccessorByNameComparer()
            {
            }

            public int Compare(OePropertyAccessor x, OePropertyAccessor y)
            {
                return String.CompareOrdinal(x.EdmProperty.Name, y.EdmProperty.Name);
            }
        }

        private readonly OePropertyAccessor[] _allAccessors;
        private readonly String _typeName;

        private OeEntryFactory(IEdmEntitySetBase entitySet, OePropertyAccessor[] accessors)
        {
            Array.Sort(accessors, AccessorByNameComparer.Instance);
            EntitySet = entitySet;
            _allAccessors = accessors;
            Accessors = GetAccessorsWithoutSkiptoken(accessors);

            EntityType = entitySet.EntityType();
            NavigationLinks = Array.Empty<OeEntryFactory>();
            _typeName = EntityType.FullName();
        }
        private OeEntryFactory(IEdmEntitySetBase entitySet, OePropertyAccessor[] accessors,
            IReadOnlyList<OeEntryFactory> navigationLinks, Func<Object, Object> linkAccessor)
            : this(entitySet, accessors)
        {
            NavigationLinks = navigationLinks ?? Array.Empty<OeEntryFactory>();
            LinkAccessor = linkAccessor;
            EqualityComparer = new Infrastructure.OeEntryEqualityComparer(GetKeyExpressions(entitySet, accessors));
        }
        private OeEntryFactory(IEdmEntitySetBase entitySet, OePropertyAccessor[] accessors, ODataNestedResourceInfo resourceInfo)
            : this(entitySet, accessors)
        {
            ResourceInfo = resourceInfo;
        }
        private OeEntryFactory(IEdmEntitySetBase entitySet, OePropertyAccessor[] accessors, ODataNestedResourceInfo resourceInfo,
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
        public static OeEntryFactory CreateEntryFactory(IEdmEntitySetBase entitySet, OePropertyAccessor[] accessors)
        {
            return new OeEntryFactory(entitySet, accessors);
        }
        public static OeEntryFactory CreateEntryFactoryParent(IEdmEntitySetBase entitySet, OePropertyAccessor[] accessors,
            IReadOnlyList<OeEntryFactory> navigationLinks, Func<Object, Object> linkAccessor)
        {
            return new OeEntryFactory(entitySet, accessors, navigationLinks, linkAccessor);
        }
        public static OeEntryFactory CreateEntryFactoryNested(IEdmEntitySetBase entitySet, OePropertyAccessor[] accessors, ODataNestedResourceInfo resourceInfo,
            IReadOnlyList<OeEntryFactory> navigationLinks, Func<Object, Object> linkAccessor)
        {
            return new OeEntryFactory(entitySet, accessors, resourceInfo, navigationLinks, linkAccessor);
        }
        public ref OePropertyAccessor GetAccessorByName(String propertyName)
        {
            int left = 0;
            int right = _allAccessors.Length - 1;
            while (left <= right)
            {
                int middle = left + ((right - left) >> 1);
                int i = String.Compare(_allAccessors[middle].EdmProperty.Name, propertyName, StringComparison.OrdinalIgnoreCase);
                if (i == 0)
                    return ref _allAccessors[middle];

                if (i < 0)
                    left = middle + 1;
                else
                    right = middle - 1;
            }

            throw new InvalidOperationException("Proeprty " + propertyName + " not found in accessors");
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
        private static List<MemberExpression> GetKeyExpressions(IEdmEntitySetBase entitySet, OePropertyAccessor[] accessors)
        {
            var propertyExpressions = new List<MemberExpression>();
            foreach (IEdmStructuralProperty key in entitySet.EntityType().Key())
            {
                bool found = false;
                foreach (OePropertyAccessor accessor in accessors)
                    if (String.CompareOrdinal(accessor.EdmProperty.Name, key.Name) == 0)
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
        public Object GetValue(Object value)
        {
            return LinkAccessor == null ? value : LinkAccessor(value);
        }

        public OePropertyAccessor[] Accessors { get; }
        public bool? CountOption { get; set; }
        public IEdmEntitySetBase EntitySet { get; }
        public IEdmEntityType EntityType { get; }
        public IEqualityComparer<Object> EqualityComparer { get; }
        public Func<Object, Object> LinkAccessor { get; }
        public IReadOnlyList<OeEntryFactory> NavigationLinks { get; }
        public ODataNestedResourceInfo ResourceInfo { get; }
    }
}
