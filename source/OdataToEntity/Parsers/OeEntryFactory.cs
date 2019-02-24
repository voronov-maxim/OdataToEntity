using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
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
        private OeEntryFactory _entryFactoryFromTuple;
        private readonly String _typeName;

        public OeEntryFactory(Type clrEntityType, IEdmEntitySetBase entitySet, OePropertyAccessor[] accessors, OePropertyAccessor[] skipTokenAccessors)
        {
            Array.Sort(accessors, AccessorByNameComparer.Instance);
            ClrEntityType = clrEntityType;
            EntitySet = entitySet;
            _allAccessors = accessors;
            Accessors = GetAccessorsWithoutSkiptoken(accessors);
            SkipTokenAccessors = skipTokenAccessors;

            EdmEntityType = entitySet.EntityType();
            NavigationLinks = Array.Empty<OeEntryFactory>();
            _typeName = EdmEntityType.FullName();

            IsTuple = OeExpressionHelper.IsTupleType(accessors[0].PropertyExpression.Expression.Type);
        }
        public OeEntryFactory(ref OeEntryFactoryOptions options)
            : this(options.ClrEntityType, options.EntitySet, options.Accessors, options.SkipTokenAccessors)
        {
            CountOption = options.CountOption;
            EdmNavigationProperty = options.EdmNavigationProperty;
            LinkAccessor = options.LinkAccessor == null ? null : (Func<Object, Object>)options.LinkAccessor.Compile();
            MaxTop = options.MaxTop;
            NavigationLinks = options.NavigationLinks ?? Array.Empty<OeEntryFactory>();
            PageSize = options.PageSize;
            ResourceInfo = options.ResourceInfo;

            EqualityComparer = new Infrastructure.OeEntryEqualityComparer(GetKeyExpressions(options.EntitySet, options.Accessors));
            IsTuple |= GetIsTuple(options.LinkAccessor);
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
        private OeEntryFactory CreateEntryFactoryFromTuple(OeEntryFactory parentEntryFactory, OePropertyAccessor[] skipTokenAccessors)
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
                navigationLinks[i] = NavigationLinks[i].CreateEntryFactoryFromTuple(this, Array.Empty<OePropertyAccessor>());

            OeEntryFactoryOptions options;
            if (parentEntryFactory == null)
            {
                options = new OeEntryFactoryOptions()
                {
                    Accessors = accessors,
                    ClrEntityType = ClrEntityType,
                    EntitySet = EntitySet,
                    NavigationLinks = navigationLinks,
                    SkipTokenAccessors = skipTokenAccessors,
                };
                return new OeEntryFactory(ref options);
            }

            ParameterExpression parameter = Expression.Parameter(typeof(Object));
            UnaryExpression typedParameter = Expression.Convert(parameter, parentEntryFactory.ClrEntityType);
            MemberExpression navigationPropertyExpression = Expression.Property(typedParameter, EdmNavigationProperty.Name);
            LambdaExpression linkAccessor = Expression.Lambda(navigationPropertyExpression, parameter);

            options = new OeEntryFactoryOptions()
            {
                Accessors = accessors,
                ClrEntityType = ClrEntityType,
                EdmNavigationProperty = EdmNavigationProperty,
                EntitySet = EntitySet,
                LinkAccessor = linkAccessor,
                NavigationLinks = navigationLinks,
                ResourceInfo = ResourceInfo,
                SkipTokenAccessors = skipTokenAccessors,
            };
            return new OeEntryFactory(ref options);
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
        public OeEntryFactory GetEntryFactoryFromTuple(IEdmModel edmModel, OrderByClause orderByClause)
        {
            if (_entryFactoryFromTuple == null)
            {
                OePropertyAccessor[] skipTokenAccessors = Array.Empty<OePropertyAccessor>();
                if (IsTuple)
                {
                    if (SkipTokenAccessors.Length > 0)
                        skipTokenAccessors = GetSkipTokenAccessors(edmModel, orderByClause);
                    else
                        skipTokenAccessors = Array.Empty<OePropertyAccessor>();
                }
                else
                    skipTokenAccessors = SkipTokenAccessors;

                _entryFactoryFromTuple = CreateEntryFactoryFromTuple(null, skipTokenAccessors);
            }
            return _entryFactoryFromTuple;
        }
        private static bool GetIsTuple(LambdaExpression linkAccessor)
        {
            if (linkAccessor != null && ((MemberExpression)linkAccessor.Body).Expression is UnaryExpression convertExpression)
                return OeExpressionHelper.IsTupleType(convertExpression.Type);

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
        private OePropertyAccessor[] GetSkipTokenAccessors(IEdmModel edmModel, OrderByClause orderByClause)
        {
            ParameterExpression parameter = Expression.Parameter(typeof(Object));
            UnaryExpression instance = Expression.Convert(parameter, ClrEntityType);
            var visitor = new OeQueryNodeVisitor(edmModel, Expression.Parameter(ClrEntityType));

            var skipTokenAccessors = new List<OePropertyAccessor>();
            while (orderByClause != null)
            {
                var propertyNode = (SingleValuePropertyAccessNode)orderByClause.Expression;
                var propertyExpression = (MemberExpression)visitor.Visit(propertyNode);
                propertyExpression = OeExpressionHelper.ReplaceParameter(propertyExpression, parameter);
                skipTokenAccessors.Add(OePropertyAccessor.CreatePropertyAccessor(propertyNode.Property, propertyExpression, parameter, true));

                orderByClause = orderByClause.ThenBy;
            }

            return skipTokenAccessors.ToArray();
        }
        public Object GetValue(Object value)
        {
            return LinkAccessor == null ? value : LinkAccessor(value);
        }

        public OePropertyAccessor[] Accessors { get; }
        public Type ClrEntityType { get; }
        public bool? CountOption { get; }
        public IEdmEntitySetBase EntitySet { get; }
        public IEdmEntityType EdmEntityType { get; }
        public IEdmNavigationProperty EdmNavigationProperty { get; }
        public IEqualityComparer<Object> EqualityComparer { get; }
        public bool IsTuple { get; }
        public Func<Object, Object> LinkAccessor { get; }
        public int MaxTop { get; }
        public IReadOnlyList<OeEntryFactory> NavigationLinks { get; }
        public int PageSize { get; }
        public ODataNestedResourceInfo ResourceInfo { get; }
        public OePropertyAccessor[] SkipTokenAccessors { get; }
    }

    public struct OeEntryFactoryOptions
    {
        public OePropertyAccessor[] Accessors { get; set; }
        public Type ClrEntityType { get; set; }
        public bool? CountOption { get; set; }
        public IEdmEntitySetBase EntitySet { get; set; }
        public IEdmNavigationProperty EdmNavigationProperty { get; set; }
        public LambdaExpression LinkAccessor { get; set; }
        public int MaxTop { get; set; }
        public IReadOnlyList<OeEntryFactory> NavigationLinks { get; set; }
        public int PageSize { get; set; }
        public ODataNestedResourceInfo ResourceInfo { get; set; }
        public OePropertyAccessor[] SkipTokenAccessors { get; set; }
    }
}