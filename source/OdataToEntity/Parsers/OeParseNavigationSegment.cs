using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System;
using System.Collections.Generic;

namespace OdataToEntity.Parsers
{
    public readonly struct OeParseNavigationSegment
    {
        public OeParseNavigationSegment(NavigationPropertySegment navigationSegment, FilterClause filter)
        {
            NavigationSegment = navigationSegment;
            Filter = filter;
        }

        private static FilterClause CreateFilterClause(IEdmEntitySet entitySet, IEnumerable<KeyValuePair<String, Object>> keys)
        {
            ResourceRangeVariableReferenceNode refNode = OeEdmClrHelper.CreateRangeVariableReferenceNode(entitySet);
            var entityType = (IEdmEntityType)refNode.RangeVariable.TypeReference.Definition;

            var propertyValues = new List<KeyValuePair<IEdmStructuralProperty, Object>>();
            foreach (KeyValuePair<String, Object> keyValue in keys)
            {
                var property = (IEdmStructuralProperty)entityType.FindProperty(keyValue.Key);
                propertyValues.Add(new KeyValuePair<IEdmStructuralProperty, Object>(property, keyValue.Value));
            }

            return new FilterClause(OeGetParser.CreateFilterExpression(refNode, propertyValues), refNode.RangeVariable);
        }
        public static IEdmEntitySet GetEntitySet(IReadOnlyList<OeParseNavigationSegment> navigationSegments)
        {
            for (int i = navigationSegments.Count - 1; i >= 0; i--)
                if (navigationSegments[i].NavigationSegment != null)
                    return (IEdmEntitySet)navigationSegments[i].NavigationSegment.NavigationSource;

            return null;
        }
        public static IReadOnlyList<OeParseNavigationSegment> GetNavigationSegments(ODataPath path)
        {
            var navigationSegments = new List<OeParseNavigationSegment>();

            ODataPathSegment previousSegment = null;
            foreach (ODataPathSegment segment in path)
            {
                if (segment is NavigationPropertySegment navigationSegment)
                    navigationSegments.Add(new OeParseNavigationSegment(navigationSegment, null));
                else if (segment is KeySegment keySegment)
                {
                    IEdmEntitySet previousEntitySet;
                    navigationSegment = null;
                    if (previousSegment is EntitySetSegment)
                    {
                        var previousEntitySetSegment = previousSegment as EntitySetSegment;
                        previousEntitySet = previousEntitySetSegment.EntitySet;
                    }
                    else if (previousSegment is NavigationPropertySegment)
                    {
                        navigationSegment = previousSegment as NavigationPropertySegment;
                        previousEntitySet = (IEdmEntitySet)navigationSegment.NavigationSource;
                    }
                    else
                        throw new InvalidOperationException("invalid segment");

                    FilterClause keyFilter = CreateFilterClause(previousEntitySet, keySegment.Keys);
                    navigationSegments.Add(new OeParseNavigationSegment(navigationSegment, keyFilter));
                }
                previousSegment = segment;
            }

            return navigationSegments;
        }

        public FilterClause Filter { get; }
        public NavigationPropertySegment NavigationSegment { get; }
    }
}
