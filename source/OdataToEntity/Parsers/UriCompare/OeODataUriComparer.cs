using Microsoft.OData;
using Microsoft.OData.UriParser;
using Microsoft.OData.UriParser.Aggregation;
using System;
using System.Collections.Generic;

namespace OdataToEntity.Parsers.UriCompare
{
    public struct OeODataUriComparer
    {
        private readonly OeODataUriComparerParameterValues _parameterValues;
        private readonly OeQueryNodeComparer _queryNodeComparer;

        public OeODataUriComparer(IReadOnlyDictionary<ConstantNode, Db.OeQueryCacheDbParameterDefinition> constantToParameterMapper)
        {
            _parameterValues = new OeODataUriComparerParameterValues(constantToParameterMapper);
            _queryNodeComparer = new OeQueryNodeComparer(_parameterValues);
        }

        public bool Compare(OeParseUriContext parseUriContext1, OeParseUriContext parseUriContext2)
        {
            if (parseUriContext1.EntitySet != parseUriContext2.EntitySet)
                return false;

            ODataUri uri1 = parseUriContext1.ODataUri;
            ODataUri uri2 = parseUriContext2.ODataUri;

            if (!ODataPathComparer.Compare(uri1.Path, uri2.Path))
                return false;

            if (!CompareParseNavigationSegments(parseUriContext1.ParseNavigationSegments, parseUriContext2.ParseNavigationSegments))
                return false;

            if (!CompareApply(uri1.Apply, uri2.Apply))
                return false;

            if (!CompareFilter(uri1.Filter, uri2.Filter))
                return false;

            if (!CompareSelectAndExpand(uri1.SelectAndExpand, uri2.SelectAndExpand))
                return false;

            if (!CompareOrderBy(uri1.OrderBy, uri2.OrderBy))
                return false;

            if (!CompareSkip(uri1.Skip, uri2.Skip))
                return false;

            if (!CompareTop(uri1.Top, uri2.Top))
                return false;

            if (!CompareHeaders(parseUriContext1.Headers, parseUriContext2.Headers))
                return false;

            return true;
        }
        private bool CompareAggregate(AggregateTransformationNode node1, AggregateTransformationNode node2)
        {
            if (node1 == node2)
                return true;
            if (node1 == null || node2 == null)
                return false;

            return EnumerableComparer.Compare(node1.Expressions, node2.Expressions, CompareAggregate);
        }
        private bool CompareAggregate(AggregateExpression expression1, AggregateExpression expression2)
        {
            return expression1.Alias == expression2.Alias &&
                expression1.Method == expression2.Method &&
                expression1.TypeReference.IsEqual(expression2.TypeReference) &&
                _queryNodeComparer.Compare(expression1.Expression, expression2.Expression);
        }
        private bool CompareApply(ApplyClause clause1, ApplyClause clause2)
        {
            if (clause1 == clause2)
                return true;
            if (clause1 == null || clause2 == null)
                return false;

            return EnumerableComparer.Compare(clause1.Transformations, clause2.Transformations, CompareTransformation);
        }
        private bool CompareFilter(FilterClause clause1, FilterClause clause2)
        {
            if (clause1 == clause2)
                return true;
            if (clause1 == null || clause2 == null)
                return false;

            if (!clause1.ItemType.IsEqual(clause1.ItemType))
                return false;
            if (!_queryNodeComparer.Compare(clause1.RangeVariable, clause2.RangeVariable))
                return false;
            return _queryNodeComparer.Compare(clause1.Expression, clause2.Expression);
        }
        private bool CompareGroupBy(GroupByTransformationNode transformation1, GroupByTransformationNode transformation2)
        {
            if (transformation1 == transformation2)
                return true;
            if (transformation1 == null || transformation2 == null)
                return false;

            if (!_queryNodeComparer.Compare(transformation1.Source, transformation2.Source))
                return false;

            if (!CompareAggregate(transformation1.ChildTransformations as AggregateTransformationNode, transformation2.ChildTransformations as AggregateTransformationNode))
                return false;

            if (!EnumerableComparer.Compare(transformation1.GroupingProperties, transformation2.GroupingProperties, CompareGroupByPropertyNode))
                return false;

            return true;
        }
        private bool CompareGroupByPropertyNode(GroupByPropertyNode node1, GroupByPropertyNode node2)
        {
            if (node1 == node2)
                return true;
            if (node1 == null || node2 == null)
                return false;

            return _queryNodeComparer.Compare(node1.Expression, node2.Expression) &&
                EnumerableComparer.Compare(node1.ChildTransformations, node2.ChildTransformations, CompareGroupByPropertyNode);
        }
        private bool CompareHeaders(OeRequestHeaders headers1, OeRequestHeaders headers2)
        {
            if (headers1 == headers2)
                return true;
            if (headers1 == null || headers2 == null)
                return false;

            return headers1.Charset == headers2.Charset &&
                headers1.ContentType == headers2.ContentType &&
                headers1.MetadataLevel == headers2.MetadataLevel &&
                headers1.Streaming == headers2.Streaming;
        }
        private bool CompareLevelsClause(LevelsClause level1, LevelsClause level2)
        {
            if (level1 == level2)
                return true;
            if (level1 == null || level2 == null)
                return false;

            return level1.IsMaxLevel == level2.IsMaxLevel && level1.Level == level2.Level;
        }
        private bool CompareOrderBy(OrderByClause clause1, OrderByClause clause2)
        {
            if (clause1 == clause2)
                return true;
            if (clause1 == null || clause2 == null)
                return false;

            return clause1.Direction == clause2.Direction &&
                clause1.ItemType.IsEqual(clause2.ItemType) &&
                _queryNodeComparer.Compare(clause1.RangeVariable, clause2.RangeVariable) &&
                _queryNodeComparer.Compare(clause1.Expression, clause2.Expression) &&
                CompareOrderBy(clause1.ThenBy, clause2.ThenBy);
        }
        private bool CompareParseNavigationSegments(IReadOnlyList<OeParseNavigationSegment> parseNavigationSegments1,
            IReadOnlyList<OeParseNavigationSegment> parseNavigationSegments2)
        {
            if (parseNavigationSegments1 == parseNavigationSegments2)
                return true;
            if (parseNavigationSegments1 == null || parseNavigationSegments2 == null)
                return false;

            if (parseNavigationSegments1.Count != parseNavigationSegments2.Count)
                return false;

            for (int i = 0; i < parseNavigationSegments1.Count; i++)
            {
                if (parseNavigationSegments1[i].NavigationSegment != parseNavigationSegments2[i].NavigationSegment)
                {
                    if (parseNavigationSegments1[i].NavigationSegment == null || parseNavigationSegments2[i].NavigationSegment == null)
                        return false;
                    if (parseNavigationSegments1[i].NavigationSegment.NavigationProperty != parseNavigationSegments2[i].NavigationSegment.NavigationProperty)
                        return false;
                }

                if (!CompareFilter(parseNavigationSegments1[i].Filter, parseNavigationSegments2[i].Filter))
                    return false;
            }

            return true;
        }
        private bool CompareSelectAndExpand(SelectExpandClause clause1, SelectExpandClause clause2)
        {
            if (clause1 == clause2)
                return true;
            if (clause1 == null || clause2 == null)
                return false;

            return clause1.AllSelected == clause2.AllSelected &&
                EnumerableComparer.Compare(clause1.SelectedItems, clause2.SelectedItems, CompareSelectItem);
        }
        private bool CompareSelectItem(SelectItem selectItem1, SelectItem selectItem2)
        {
            if (selectItem1.GetType() != selectItem2.GetType())
                return false;

            if (selectItem1 is ExpandedNavigationSelectItem)
            {
                var expand1 = selectItem1 as ExpandedNavigationSelectItem;
                var expand2 = selectItem2 as ExpandedNavigationSelectItem;

                return CompareLevelsClause(expand1.LevelsOption, expand2.LevelsOption) &&
                    expand1.CountOption == expand2.CountOption &&
                    CompareFilter(expand1.FilterOption, expand2.FilterOption) &&
                    expand1.NavigationSource == expand2.NavigationSource &&
                    CompareOrderBy(expand1.OrderByOption, expand2.OrderByOption) &&
                    ODataPathComparer.Compare(expand1.PathToNavigationProperty, expand2.PathToNavigationProperty) &&
                    CompareSelectAndExpand(expand1.SelectAndExpand, expand2.SelectAndExpand) &&
                    expand1.SkipOption == expand2.SkipOption &&
                    expand1.TopOption == expand2.TopOption;
            }
            else if (selectItem1 is PathSelectItem)
            {
                var path1 = selectItem1 as PathSelectItem;
                var path2 = selectItem2 as PathSelectItem;
                return ODataPathComparer.Compare(path1.SelectedPath, path2.SelectedPath);
            }
            else
                throw new NotSupportedException();
        }
        private bool CompareSkip(long? skip1, long? skip2)
        {
            if (skip1 == skip2)
            {
                if (skip1 == null || skip2 == null)
                    return true;
            }
            else
            {
                if (skip1 == null || skip2 == null)
                    return false;
            }

            _parameterValues.AddSkipParameter(skip2.Value);
            return true;
        }
        private bool CompareTop(long? top1, long? top2)
        {
            if (top1 == top2)
            {
                if (top1 == null || top2 == null)
                    return true;
            }
            else
            {
                if (top1 == null || top2 == null)
                    return false;
            }

            _parameterValues.AddTopParameter(top2.Value);
            return true;
        }
        private bool CompareTransformation(TransformationNode node1, TransformationNode node2)
        {
            if (node1.GetType() != node2.GetType())
                return false;

            if (node1 is GroupByTransformationNode)
            {
                if (!CompareGroupBy(node1 as GroupByTransformationNode, node2 as GroupByTransformationNode))
                    return false;
            }
            else if (node1 is AggregateTransformationNode)
            {
                if (!CompareAggregate(node1 as AggregateTransformationNode, node2 as AggregateTransformationNode))
                    return false;
            }
            else if (node1 is FilterTransformationNode)
            {
                FilterClause filter1 = (node1 as FilterTransformationNode).FilterClause;
                FilterClause filter2 = (node2 as FilterTransformationNode).FilterClause;
                if (!CompareFilter(filter1, filter2))
                    return false;
            }
            else
            {
                throw new NotSupportedException();
            }

            return true;
        }

        public IReadOnlyList<Db.OeQueryCacheDbParameterValue> ParameterValues => _parameterValues.ParameterValues;
    }
}
