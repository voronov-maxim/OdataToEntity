using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace OdataToEntity.Parsers
{
    public class OeEntryFactory
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
        private OeEntryFactory? _entryFactoryFromTuple;
        private readonly IEqualityComparer<Object>? _equalityComparer;
        private readonly String _typeName;

        public OeEntryFactory(
            IEdmEntitySetBase entitySet,
            OePropertyAccessor[] accessors,
            OePropertyAccessor[]? skipTokenAccessors)
        {
            Array.Sort(accessors, AccessorByNameComparer.Instance);
            EntitySet = entitySet;
            _allAccessors = accessors;
            Accessors = GetAccessorsWithoutSkiptoken(accessors);
            SkipTokenAccessors = skipTokenAccessors ?? Array.Empty<OePropertyAccessor>();

            EdmEntityType = entitySet.EntityType();
            NavigationLinks = Array.Empty<OeNavigationEntryFactory>();
            _typeName = EdmEntityType.FullName();

            if (accessors.Length != 0 && accessors[0].PropertyExpression is MemberExpression propertyExpression)
                IsTuple = OeExpressionHelper.IsTupleType(propertyExpression.Expression!.Type);
        }
        public OeEntryFactory(
            IEdmEntitySetBase entitySet,
            OePropertyAccessor[] accessors,
            OePropertyAccessor[]? skipTokenAccessors,
            IReadOnlyList<OeNavigationEntryFactory>? navigationLinks)
            : this(entitySet, accessors, skipTokenAccessors, navigationLinks, null)
        {
        }
        public OeEntryFactory(
            IEdmEntitySetBase entitySet,
            OePropertyAccessor[] accessors,
            OePropertyAccessor[]? skipTokenAccessors,
            IReadOnlyList<OeNavigationEntryFactory>? navigationLinks,
            LambdaExpression? linkAccessor)
            : this(entitySet, accessors, skipTokenAccessors)
        {

            NavigationLinks = navigationLinks ?? Array.Empty<OeNavigationEntryFactory>();

            if (linkAccessor != null)
            {
                IsTuple = GetIsTuple(linkAccessor);
                LinkAccessor = (Func<Object, Object>)linkAccessor.Compile();
            }
            if (accessors.Length > 0)
                _equalityComparer = new Infrastructure.OeEntryEqualityComparer(GetKeyExpressions(entitySet, accessors));
        }

        public ODataResource CreateEntry(Object? entity)
        {
            var odataProperties = new ODataProperty[Accessors.Length];
            for (int i = 0; i < Accessors.Length; i++)
            {
                Object? value = Accessors[i].GetValue(entity);
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
        protected virtual OeNavigationEntryFactory CreateEntryFactoryFromTuple(IEdmModel edmModel, OeEntryFactory parentEntryFactory)
        {
            throw new InvalidOperationException("Must be invoke for " + nameof(OeNavigationEntryFactory));
        }
        private OeEntryFactory CreateEntryFactoryFromTuple(IEdmModel edmModel, OePropertyAccessor[]? skipTokenAccessors)
        {
            OePropertyAccessor[] accessors = GetAccessorsFromTuple(edmModel);
            var navigationLinks = new OeNavigationEntryFactory[NavigationLinks.Count];
            for (int i = 0; i < NavigationLinks.Count; i++)
                navigationLinks[i] = NavigationLinks[i].CreateEntryFactoryFromTuple(edmModel, this);

            return new OeEntryFactory(EntitySet, accessors, skipTokenAccessors, navigationLinks);
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

            throw new InvalidOperationException("Property " + propertyName + " not found in accessors");
        }
        protected OePropertyAccessor[] GetAccessorsFromTuple(IEdmModel edmModel)
        {
            OePropertyAccessor[] accessors = _allAccessors;
            if (IsTuple)
            {
                OePropertyAccessor[] propertyAccessors = OePropertyAccessor.CreateFromType(edmModel.GetClrType(EntitySet), EntitySet);
                accessors = new OePropertyAccessor[_allAccessors.Length];
                for (int i = 0; i < accessors.Length; i++)
                {
                    OePropertyAccessor accessor = Array.Find(propertyAccessors, pa => pa.EdmProperty == _allAccessors[i].EdmProperty);
                    if (Array.IndexOf(Accessors, _allAccessors[i]) == -1)
                    {
                        var propertyExpression = (MemberExpression)accessor.PropertyExpression;
                        var convertExpression = (UnaryExpression)propertyExpression.Expression!;
                        var parameterExpression = (ParameterExpression)convertExpression.Operand;
                        accessor = OePropertyAccessor.CreatePropertyAccessor(accessor.EdmProperty, propertyExpression, parameterExpression, true);
                    }
                    accessors[i] = accessor;
                }
            }
            return accessors;
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
        public OeEntryFactory GetEntryFactoryFromTuple(IEdmModel edmModel, OrderByClause? orderByClause)
        {
            if (_entryFactoryFromTuple == null)
            {
                OePropertyAccessor[]? skipTokenAccessors = null;
                if (IsTuple)
                {
                    if (SkipTokenAccessors.Length > 0)
                        skipTokenAccessors = GetSkipTokenAccessors(edmModel, orderByClause);
                }
                else
                    skipTokenAccessors = SkipTokenAccessors;

                _entryFactoryFromTuple = CreateEntryFactoryFromTuple(edmModel, skipTokenAccessors);
            }
            return _entryFactoryFromTuple;
        }
        private static bool GetIsTuple(LambdaExpression? linkAccessor)
        {
            if (linkAccessor != null && ((MemberExpression)linkAccessor.Body).Expression is UnaryExpression convertExpression)
                return OeExpressionHelper.IsTupleType(convertExpression.Type);

            return false;
        }
        private static List<Expression> GetKeyExpressions(IEdmEntitySetBase entitySet, OePropertyAccessor[] accessors)
        {
            var propertyExpressions = new List<Expression>();
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
        private OePropertyAccessor[] GetSkipTokenAccessors(IEdmModel edmModel, OrderByClause? orderByClause)
        {
            ParameterExpression parameter = Expression.Parameter(typeof(Object));
            Type clrEntityType = edmModel.GetClrType(EntitySet);
            var visitor = new OeQueryNodeVisitor(Expression.Parameter(clrEntityType));

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
        public Object? GetValue(Object? value)
        {
            if (value == null)
                return null;

            return LinkAccessor == null ? value : LinkAccessor(value);
        }

        public OePropertyAccessor[] Accessors { get; }
        public IEdmEntitySetBase EntitySet { get; }
        public IEdmEntityType EdmEntityType { get; }
        public IEqualityComparer<Object> EqualityComparer => _equalityComparer ?? throw new InvalidOperationException(nameof(EqualityComparer) + " is null");
        public bool IsTuple { get; }
        public Func<Object, Object>? LinkAccessor { get; }
        public IReadOnlyList<OeNavigationEntryFactory> NavigationLinks { get; }
        public OePropertyAccessor[] SkipTokenAccessors { get; }
    }
}