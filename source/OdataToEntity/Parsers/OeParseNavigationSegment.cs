using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System;
using System.Collections.Generic;

namespace OdataToEntity.Parsers
{
    public readonly struct OeParseNavigationSegment
    {
        public OeParseNavigationSegment(NavigationPropertySegment? navigationSegment, FilterClause? filter)
        {
            if (navigationSegment == null && filter == null)
                throw new InvalidOperationException("navigationSegment or filter must be not null");

            NavigationSegment = navigationSegment;
            Filter = filter;
        }

        private static FilterClause CreateFilterClause(IEdmEntitySet entitySet, IEnumerable<KeyValuePair<String, Object>> keys)
        {
            ResourceRangeVariableReferenceNode refNode = OeEdmClrHelper.CreateRangeVariableReferenceNode(entitySet);
            var entityType = (IEdmEntityType)refNode.RangeVariable.TypeReference.Definition;

            var propertyValues = new List<KeyValuePair<IEdmStructuralProperty, Object?>>();
            foreach (KeyValuePair<String, Object> keyValue in keys)
            {
                var property = (IEdmStructuralProperty)entityType.GetPropertyIgnoreCase(keyValue.Key);
                propertyValues.Add(new KeyValuePair<IEdmStructuralProperty, Object?>(property, keyValue.Value));
            }

            return new FilterClause(OeExpressionHelper.CreateFilterExpression(refNode, propertyValues), refNode.RangeVariable);
        }
        public static IEdmEntitySet? GetEntitySet(IReadOnlyList<OeParseNavigationSegment> navigationSegments)
        {
            for (int i = navigationSegments.Count - 1; i >= 0; i--)
            {
                NavigationPropertySegment? navigationSegment = navigationSegments[i].NavigationSegment;
                if (navigationSegment != null)
                    return (IEdmEntitySet?)navigationSegment.NavigationSource;
            }

            return null;
        }
        private static IEdmEntitySet GetEntitySet(ODataPathSegment previousSegment, out NavigationPropertySegment? navigationPropertySegment)
        {
            navigationPropertySegment = null;
            if (previousSegment is EntitySetSegment entitySetSegment)
                return entitySetSegment.EntitySet;

            navigationPropertySegment = previousSegment as NavigationPropertySegment;
            if (navigationPropertySegment != null)
                return (IEdmEntitySet)navigationPropertySegment.NavigationSource;

            throw new InvalidOperationException("Invalid segment");
        }
        public static IReadOnlyList<OeParseNavigationSegment> GetNavigationSegments(ODataPath path)
        {
            var navigationSegments = new List<OeParseNavigationSegment>();

            ODataPathSegment? previousSegment = null;
            foreach (ODataPathSegment segment in path)
            {
                if (segment is NavigationPropertySegment navigationSegment)
                    navigationSegments.Add(new OeParseNavigationSegment(navigationSegment, null));
                else if (segment is KeySegment keySegment)
                {
                    if (previousSegment == null)
                        throw new InvalidOperationException("Before KeySegment must be other segment");

                    IEdmEntitySet entitySet = GetEntitySet(previousSegment, out NavigationPropertySegment? previousNavigationSegment);
                    FilterClause keyFilter = CreateFilterClause(entitySet, keySegment.Keys);
                    navigationSegments.Add(new OeParseNavigationSegment(previousNavigationSegment, keyFilter));
                }
                else if (segment is FilterSegment filterSegment)
                {
                    if (previousSegment == null)
                        throw new InvalidOperationException("Before FilterSegment must be other segment");

                    GetEntitySet(previousSegment, out NavigationPropertySegment? previousNavigationSegment);
                    FilterClause filterClause = new FilterClause(filterSegment.Expression, filterSegment.RangeVariable);
                    navigationSegments.Add(new OeParseNavigationSegment(previousNavigationSegment, filterClause));
                }

                previousSegment = segment;
            }

            return navigationSegments;
        }

        public FilterClause? Filter { get; }
        public NavigationPropertySegment? NavigationSegment { get; }
    }
}
