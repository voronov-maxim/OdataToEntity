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

        private OeEntryFactory(Type clrEntityType, IEdmEntitySetBase entitySet, OePropertyAccessor[] accessors)
        {
            Array.Sort(accessors, AccessorByNameComparer.Instance);
            ClrEntityType = clrEntityType;
            EntitySet = entitySet;
            _allAccessors = accessors;
            Accessors = GetAccessorsWithoutSkiptoken(accessors);

            EdmEntityType = entitySet.EntityType();
            NavigationLinks = Array.Empty<OeEntryFactory>();
            _typeName = EdmEntityType.FullName();
        }
        private OeEntryFactory(Type clrEntityType, IEdmEntitySetBase entitySet, OePropertyAccessor[] accessors,
            IReadOnlyList<OeEntryFactory> navigationLinks, LambdaExpression linkAccessor)
            : this(clrEntityType, entitySet, accessors)
        {
            NavigationLinks = navigationLinks ?? Array.Empty<OeEntryFactory>();
            LinkAccessor = linkAccessor == null ? null : (Func<Object, Object>)linkAccessor.Compile();
            EqualityComparer = new Infrastructure.OeEntryEqualityComparer(GetKeyExpressions(entitySet, accessors));
            IsTuple = GetIsTuple(linkAccessor);
        }
        private OeEntryFactory(Type clrEntityType, IEdmEntitySetBase entitySet, OePropertyAccessor[] accessors, ODataNestedResourceInfo resourceInfo)
            : this(clrEntityType, entitySet, accessors)
        {
            ResourceInfo = resourceInfo;
        }
        private OeEntryFactory(Type clrEntityType, IEdmEntitySetBase entitySet, OePropertyAccessor[] accessors,
            IReadOnlyList<OeEntryFactory> navigationLinks, LambdaExpression linkAccessor, ODataNestedResourceInfo resourceInfo)
            : this(clrEntityType, entitySet, accessors)
        {
            NavigationLinks = navigationLinks ?? Array.Empty<OeEntryFactory>();
            LinkAccessor = linkAccessor == null ? null : (Func<Object, Object>)linkAccessor.Compile();
            EqualityComparer = new Infrastructure.OeEntryEqualityComparer(GetKeyExpressions(entitySet, accessors));
            ResourceInfo = resourceInfo;
            IsTuple = GetIsTuple(linkAccessor);
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
        public static OeEntryFactory CreateEntryFactory(Type clrEntityType, IEdmEntitySetBase entitySet, OePropertyAccessor[] accessors)
        {
            return new OeEntryFactory(clrEntityType, entitySet, accessors);
        }
        public static OeEntryFactory CreateEntryFactoryParent(Type clrEntityType, IEdmEntitySetBase entitySet, OePropertyAccessor[] accessors,
            IReadOnlyList<OeEntryFactory> navigationLinks, LambdaExpression linkAccessor)
        {
            return new OeEntryFactory(clrEntityType, entitySet, accessors, navigationLinks, linkAccessor);
        }
        public static OeEntryFactory CreateEntryFactoryNested(Type clrEntityType, IEdmEntitySetBase entitySet, OePropertyAccessor[] accessors,
            IReadOnlyList<OeEntryFactory> navigationLinks, LambdaExpression linkAccessor, ODataNestedResourceInfo resourceInfo)
        {
            return new OeEntryFactory(clrEntityType, entitySet, accessors, navigationLinks, linkAccessor, resourceInfo);
        }
        public OeEntryFactory CreateEntryFactoryFromTuple()
        {
            return CreateEntryFactoryFromTuple(null);
        }
        private OeEntryFactory CreateEntryFactoryFromTuple(OeEntryFactory parentEntryFactory)
        {
            OePropertyAccessor[] accessors = _allAccessors;
            if (IsTuple)
            {
                accessors = new OePropertyAccessor[_allAccessors.Length];
                OePropertyAccessor[] propertyAccessors = OePropertyAccessor.CreateFromType(ClrEntityType, EntitySet);
                for (int i = 0; i < accessors.Length; i++)
                {
                    OePropertyAccessor accessor = Array.Find(propertyAccessors, pa => pa.EdmProperty == _allAccessors[i].EdmProperty);
                    if (Array.IndexOf(Accessors, _allAccessors[i]) == -1)
                    {
                        var convertExpression = (UnaryExpression)accessor.PropertyExpression.Expression;
                        var parameterExpression = (ParameterExpression)convertExpression.Operand;
                        accessor = OePropertyAccessor.CreatePropertyAccessor(accessor.EdmProperty, accessor.PropertyExpression, parameterExpression, true);
                    }
                    accessors[i] = accessor;
                }
            }

            var navigationLinks = new OeEntryFactory[NavigationLinks.Count];
            for (int i = 0; i < NavigationLinks.Count; i++)
                navigationLinks[i] = NavigationLinks[i].CreateEntryFactoryFromTuple(this);

            if (parentEntryFactory == null)
                return OeEntryFactory.CreateEntryFactoryParent(ClrEntityType, EntitySet, accessors, navigationLinks, null);

            ParameterExpression parameter = Expression.Parameter(typeof(Object));
            UnaryExpression typedParameter = Expression.Convert(parameter, parentEntryFactory.ClrEntityType);
            MemberExpression navigationPropertyExpression = Expression.Property(typedParameter, ResourceInfo.Name);
            LambdaExpression linkAccessor = Expression.Lambda(navigationPropertyExpression, parameter);

            return OeEntryFactory.CreateEntryFactoryNested(ClrEntityType, EntitySet, accessors, navigationLinks, linkAccessor, ResourceInfo);
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
        private static bool GetIsTuple(LambdaExpression linkAccessor)
        {
            if (linkAccessor != null && ((MemberExpression)linkAccessor.Body).Expression is UnaryExpression unaryExpression)
                return OeExpressionHelper.IsTupleType(unaryExpression.Type);

            return false;
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
        public Type ClrEntityType { get; }
        public bool? CountOption { get; set; }
        public IEdmEntitySetBase EntitySet { get; }
        public IEdmEntityType EdmEntityType { get; }
        public IEqualityComparer<Object> EqualityComparer { get; }
        public bool IsTuple { get; }
        public Func<Object, Object> LinkAccessor { get; }
        public IReadOnlyList<OeEntryFactory> NavigationLinks { get; }
        public ODataNestedResourceInfo ResourceInfo { get; }
    }
}
