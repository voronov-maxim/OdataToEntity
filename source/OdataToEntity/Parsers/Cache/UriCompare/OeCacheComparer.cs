using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using Microsoft.OData.UriParser.Aggregation;
using OdataToEntity.Parsers;
using OdataToEntity.Parsers.Translators;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OdataToEntity.Cache.UriCompare
{
    public readonly struct OeCacheComparer
    {
        private readonly OeCacheComparerParameterValues _parameterValues;
        private readonly OeQueryNodeComparer _queryNodeComparer;

        public OeCacheComparer(IReadOnlyDictionary<ConstantNode, OeQueryCacheDbParameterDefinition>? constantToParameterMapper)
        {
            _parameterValues = new OeCacheComparerParameterValues(constantToParameterMapper);
            _queryNodeComparer = new OeQueryNodeComparer(_parameterValues);
        }

        internal static int CombineHashCodes(int h1, int h2)
        {
            return (h1 << 5) + h1 ^ h2;
        }
        public bool Compare(OeCacheContext cacheContext1, OeCacheContext cacheContext2)
        {
            if (cacheContext1.NavigationNextLink != cacheContext2.NavigationNextLink)
                return false;

            if (cacheContext1.MetadataLevel != cacheContext2.MetadataLevel)
                return false;

            ODataUri uri1 = cacheContext1.ODataUri;
            ODataUri uri2 = cacheContext2.ODataUri;

            if (!ODataPathComparer.Compare(uri1.Path, uri2.Path))
                return false;

            if (!CompareParseNavigationSegments(cacheContext1.ParseNavigationSegments, cacheContext2.ParseNavigationSegments))
                return false;

            if (!CompareApply(uri1.Apply, uri2.Apply))
                return false;

            if (!CompareFilter(uri1.Filter, uri2.Filter, false))
                return false;

            if (!CompareSelectAndExpand(uri1.SelectAndExpand, uri2.SelectAndExpand, uri1.Path))
                return false;

            if (!CompareOrderBy(uri1.OrderBy, uri2.OrderBy, false))
                return false;

            if (!CompareSkip(uri1.Skip, uri2.Skip, uri1.Path))
                return false;

            if (!CompareTop(uri1.Top, uri2.Top, uri1.Path))
                return false;

            if (!CompareSkipToken(cacheContext1.SkipTokenNameValues, cacheContext2.SkipTokenNameValues))
                return false;

            if (!CompareCompute(uri1.Compute, uri2.Compute))
                return false;

            return true;
        }
        private bool CompareAggregate(AggregateTransformationNode? node1, AggregateTransformationNode? node2)
        {
            if (node1 == node2)
                return true;
            if (node1 == null || node2 == null)
                return false;

            return EnumerableComparer.Compare(node1.AggregateExpressions, node2.AggregateExpressions, CompareAggregate);
        }
        private bool CompareAggregate(AggregateExpressionBase expression1, AggregateExpressionBase expression2)
        {
            if (expression1.AggregateKind != expression2.AggregateKind)
                return false;

            if (expression1 is AggregateExpression aggregateExpression1 && expression2 is AggregateExpression aggregateExpression2)
                return CompareAggregate(aggregateExpression1, aggregateExpression2);

            if (expression1 is EntitySetAggregateExpression entitySetAggExpression1 && expression2 is EntitySetAggregateExpression entitySetAggExpression2)
                return CompareAggregate(entitySetAggExpression1, entitySetAggExpression2);

            throw new NotSupportedException("Unknown aggregate expression type " + expression1.GetType().Name);
        }
        private bool CompareAggregate(AggregateExpression expression1, AggregateExpression expression2)
        {
            return expression1.Alias == expression2.Alias &&
                expression1.Method == expression2.Method &&
                expression1.TypeReference.IsEqual(expression2.TypeReference) &&
                _queryNodeComparer.Compare(expression1.Expression, expression2.Expression);
        }
        private bool CompareAggregate(EntitySetAggregateExpression expression1, EntitySetAggregateExpression expression2)
        {
            return expression1.Alias == expression2.Alias &&
                _queryNodeComparer.Compare(expression1.Expression, expression2.Expression) &&
                EnumerableComparer.Compare(expression1.Children, expression2.Children, CompareAggregate);

        }
        private bool CompareApply(ApplyClause clause1, ApplyClause clause2)
        {
            if (clause1 == clause2)
                return true;
            if (clause1 == null || clause2 == null)
                return false;

            return EnumerableComparer.Compare(clause1.Transformations, clause2.Transformations, CompareTransformation);
        }
        private bool CompareCompute(ComputeClause clause1, ComputeClause clause2)
        {
            if (clause1 == clause2)
                return true;
            if (clause1 == null || clause2 == null)
                return false;

            return EnumerableComparer.Compare(clause1.ComputedItems, clause2.ComputedItems, CompareComputeExpression);
        }
        private bool CompareComputeExpression(ComputeExpression expression1, ComputeExpression expression2)
        {
            if (expression1.Alias != expression2.Alias)
                return false;

            return expression1.TypeReference.IsEqual(expression2.TypeReference) &&
                _queryNodeComparer.Compare(expression1.Expression, expression2.Expression);
        }
        private bool CompareComputeTransformation(ComputeTransformationNode? node1, ComputeTransformationNode? node2)
        {
            if (node1 == node2)
                return true;
            if (node1 == null || node2 == null)
                return false;

            return EnumerableComparer.Compare(node1.Expressions, node2.Expressions, CompareComputeExpression);
        }
        private bool CompareFilter(FilterClause? clause1, FilterClause? clause2, bool navigationNextLink)
        {
            if (clause1 == clause2)
                return true;
            if (clause1 == null || clause2 == null)
                return false;

            if (!clause1.ItemType.IsEqual(clause2.ItemType))
                return false;

            OeQueryNodeComparer queryNodeComparer = navigationNextLink ? new OeQueryNodeComparer(default) : _queryNodeComparer;
            if (!queryNodeComparer.Compare(clause1.RangeVariable, clause2.RangeVariable))
                return false;

            return queryNodeComparer.Compare(clause1.Expression, clause2.Expression);
        }
        private bool CompareGroupBy(GroupByTransformationNode? transformation1, GroupByTransformationNode? transformation2)
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
        private static bool CompareLevelsClause(LevelsClause level1, LevelsClause level2)
        {
            if (level1 == level2)
                return true;
            if (level1 == null || level2 == null)
                return false;

            return level1.IsMaxLevel == level2.IsMaxLevel && level1.Level == level2.Level;
        }
        private bool CompareOrderBy(OrderByClause clause1, OrderByClause clause2, bool navigationNextLink)
        {
            if (clause1 == clause2)
                return true;
            if (clause1 == null || clause2 == null)
                return false;

            OeQueryNodeComparer queryNodeComparer = navigationNextLink ? new OeQueryNodeComparer(default) : _queryNodeComparer;
            return clause1.Direction == clause2.Direction &&
                clause1.ItemType.IsEqual(clause2.ItemType) &&
                queryNodeComparer.Compare(clause1.RangeVariable, clause2.RangeVariable) &&
                queryNodeComparer.Compare(clause1.Expression, clause2.Expression) &&
                CompareOrderBy(clause1.ThenBy, clause2.ThenBy, navigationNextLink);
        }
        private bool CompareParseNavigationSegments(IReadOnlyList<OeParseNavigationSegment>? parseNavigationSegments1,
            IReadOnlyList<OeParseNavigationSegment>? parseNavigationSegments2)
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
                    NavigationPropertySegment? navigationSegment1 = parseNavigationSegments1[i].NavigationSegment;
                    NavigationPropertySegment? navigationSegment2 = parseNavigationSegments2[i].NavigationSegment;
                    if (navigationSegment1 == null || navigationSegment2 == null)
                        return false;
                    if (navigationSegment1.NavigationProperty != navigationSegment2.NavigationProperty)
                        return false;
                }

                if (!CompareFilter(parseNavigationSegments1[i].Filter, parseNavigationSegments2[i].Filter, false))
                    return false;
            }

            return true;
        }
        private bool CompareSelectAndExpand(SelectExpandClause clause1, SelectExpandClause clause2, ODataPath path)
        {
            if (clause1 == null && clause2 == null)
                return true;
            if (clause1 == null || clause2 == null)
                return false;

            return clause1.AllSelected == clause2.AllSelected &&
                EnumerableComparer.Compare(clause1.SelectedItems, clause2.SelectedItems, path, CompareSelectItem);
        }
        private bool CompareSelectItem(SelectItem selectItem1, SelectItem selectItem2, ODataPath path)
        {
            if (selectItem1.GetType() != selectItem2.GetType())
                return false;

            if (selectItem1 is ExpandedNavigationSelectItem expandItem1)
            {
                var expandItem2 = (ExpandedNavigationSelectItem)selectItem2;

                if (!CompareLevelsClause(expandItem1.LevelsOption, expandItem2.LevelsOption))
                    return false;

                if (expandItem1.CountOption != expandItem2.CountOption)
                    return false;

                if (expandItem1.NavigationSource != expandItem2.NavigationSource)
                    return false;

                bool navigationNextLink1 = expandItem1.SelectAndExpand.IsNextLink();
                bool navigationNextLink2 = expandItem2.SelectAndExpand.IsNextLink();
                if (navigationNextLink1 != navigationNextLink2)
                    return false;

                if (!CompareFilter(expandItem1.FilterOption, expandItem2.FilterOption, navigationNextLink1))
                    return false;

                if (!CompareOrderBy(expandItem1.OrderByOption, expandItem2.OrderByOption, navigationNextLink1))
                    return false;

                if (!ODataPathComparer.Compare(expandItem1.PathToNavigationProperty, expandItem2.PathToNavigationProperty))
                    return false;

                path = new ODataPath(path.Union(expandItem2.PathToNavigationProperty));
                if (navigationNextLink1)
                {
                    if (expandItem1.SkipOption == null || expandItem2.SkipOption == null)
                        if (expandItem1.SkipOption != expandItem2.SkipOption)
                            return false;

                    if (expandItem1.TopOption == null || expandItem2.TopOption == null)
                        if (expandItem1.TopOption != expandItem2.TopOption)
                            return false;

                    return new OeCacheComparer(null).CompareSelectAndExpand(expandItem1.SelectAndExpand, expandItem2.SelectAndExpand, path);
                }

                return CompareSkip(expandItem1.SkipOption, expandItem2.SkipOption, path) &&
                    CompareTop(expandItem1.TopOption, expandItem2.TopOption, path) &&
                    CompareSelectAndExpand(expandItem1.SelectAndExpand, expandItem2.SelectAndExpand, path);
            }

            if (selectItem1 is PathSelectItem pathItem1)
            {
                var pathItem2 = (PathSelectItem)selectItem2;
                return ODataPathComparer.Compare(pathItem1.SelectedPath, pathItem2.SelectedPath);
            }

            if (selectItem1 is OePageSelectItem pageItem1)
            {
                var pageItem2 = (OePageSelectItem)selectItem2;

                if (pageItem1.PageSize == 0 && pageItem2.PageSize == 0)
                    return true;

                if (pageItem1.PageSize == 0 || pageItem2.PageSize == 0)
                    return false;

                if (pageItem2.PageSize > 0)
                    if (path.LastSegment is EntitySetSegment ||
                        path.LastSegment is NavigationPropertySegment segment && segment.NavigationProperty.Type.IsCollection())
                        _parameterValues.AddTopParameter(pageItem2.PageSize, path);

                return true;
            }

            if (selectItem1 is OeNextLinkSelectItem nextLinkItem1)
            {
                var nextLinkItem2 = (OeNextLinkSelectItem)selectItem2;
                return nextLinkItem1.NextLink == nextLinkItem2.NextLink;
            }

            throw new InvalidOperationException("Unknown SelectItem " + selectItem1.GetType().ToString());
        }
        private bool CompareSkip(long? skip1, long? skip2, ODataPath path)
        {
            if (skip1 == null || skip2 == null)
                return skip1 == skip2;

            _parameterValues.AddSkipParameter(skip2.Value, path);
            return true;
        }
        private bool CompareSkipToken(OeSkipTokenNameValue[] skipTokenNameValues1, OeSkipTokenNameValue[] skipTokenNameValues2)
        {
            if (skipTokenNameValues1 == null && skipTokenNameValues2 == null)
                return true;
            if (skipTokenNameValues1 == null || skipTokenNameValues2 == null)
                return false;

            if (skipTokenNameValues1.Length != skipTokenNameValues2.Length)
                return false;

            for (int i = 0; i < skipTokenNameValues2.Length; i++)
                if ((skipTokenNameValues1[i].Value == null || skipTokenNameValues2[i].Value == null) &&
                    skipTokenNameValues1[i].Value != skipTokenNameValues2[i].Value)
                    return false;

            for (int i = 0; i < skipTokenNameValues2.Length; i++)
                _parameterValues.AddSkipTokenParameter(skipTokenNameValues2[i].Value, skipTokenNameValues2[i].Name);
            return true;
        }
        private bool CompareTop(long? top1, long? top2, ODataPath path)
        {
            if (top1 == null || top2 == null)
                return top1 == top2;

            _parameterValues.AddTopParameter(top2.Value, path);
            return true;
        }
        private bool CompareTransformation(TransformationNode node1, TransformationNode node2)
        {
            if (node1.GetType() != node2.GetType())
                return false;

            if (node1 is GroupByTransformationNode groupByTransformation1)
            {
                if (!CompareGroupBy(groupByTransformation1, node2 as GroupByTransformationNode))
                    return false;
            }
            else if (node1 is AggregateTransformationNode aggregateTransformation1)
            {
                if (!CompareAggregate(aggregateTransformation1, node2 as AggregateTransformationNode))
                    return false;
            }
            else if (node1 is FilterTransformationNode filterTransformation1)
            {
                FilterClause filter1 = filterTransformation1.FilterClause;
                FilterClause filter2 = ((FilterTransformationNode)node2).FilterClause;
                if (!CompareFilter(filter1, filter2, false))
                    return false;
            }
            else if (node1 is ComputeTransformationNode computeTransformation1)
            {
                if (!CompareComputeTransformation(computeTransformation1, node2 as ComputeTransformationNode))
                    return false;
            }
            else
            {
                throw new NotSupportedException();
            }

            return true;
        }
        public static int GetCacheCode(OeCacheContext cacheContext)
        {
            var hashVisitor = new OeQueryNodeHashVisitor();

            ODataUri uri = cacheContext.ODataUri;
            int hash = uri.Path.FirstSegment.Identifier.GetHashCode();
            hash = CombineHashCodes(hash, uri.Path.LastSegment.Identifier.GetHashCode());

            if (cacheContext.ParseNavigationSegments != null)
                for (int i = 0; i < cacheContext.ParseNavigationSegments.Count; i++)
                {
                    OeParseNavigationSegment parseNavigationSegment = cacheContext.ParseNavigationSegments[i];
                    if (parseNavigationSegment.Filter != null)
                    {
                        int h = hashVisitor.TranslateNode(parseNavigationSegment.Filter.Expression);
                        hash = CombineHashCodes(hash, h);
                    }
                }

            if (uri.Filter != null)
                hash = CombineHashCodes(hash, hashVisitor.TranslateNode(uri.Filter.Expression));

            if (uri.Apply != null)
                hash = GetCacheCode(hash, uri.Apply, hashVisitor);

            if (uri.SelectAndExpand != null)
                hash = GetCacheCode(hash, uri.SelectAndExpand);

            if (uri.OrderBy != null)
            {
                hash = CombineHashCodes(hash, (int)uri.OrderBy.Direction);
                hash = CombineHashCodes(hash, hashVisitor.TranslateNode(uri.OrderBy.Expression));
            }

            if (uri.Compute != null)
                foreach (ComputeExpression computeExpression in uri.Compute.ComputedItems)
                    hash = CombineHashCodes(hash, computeExpression.Alias.GetHashCode());

            if (uri.SkipToken != null)
                hash = CombineHashCodes(hash, 4999559);

            return hash;
        }
        private static int GetCacheCode(int hash, ApplyClause applyClause, OeQueryNodeHashVisitor hashVisitor)
        {
            foreach (TransformationNode transformationNode in applyClause.Transformations)
            {
                if (transformationNode is FilterTransformationNode filterTransformationNode)
                {
                    FilterClause filter = filterTransformationNode.FilterClause;
                    hash = CombineHashCodes(hash, hashVisitor.TranslateNode(filter.Expression));
                }
                else if (transformationNode is AggregateTransformationNode aggregateTransformationNode)
                {
                    foreach (AggregateExpressionBase aggregateBase in aggregateTransformationNode.AggregateExpressions)
                        if (aggregateBase is AggregateExpression aggregate)
                            hash = CombineHashCodes(hash, (int)aggregate.Method);
                        else if (aggregateBase is EntitySetAggregateExpression entitySetAggregate)
                            hash = CombineHashCodes(hash, entitySetAggregate.Alias.GetHashCode());
                        else
                            throw new NotSupportedException("Unknown aggregate expression type " + aggregateBase.GetType().Name);
                }
                else if (transformationNode is GroupByTransformationNode groupByTransformationNode)
                {
                    foreach (GroupByPropertyNode group in groupByTransformationNode.GroupingProperties)
                        hash = CombineHashCodes(hash, group.Name.GetHashCode());
                }
                else if (transformationNode is ComputeTransformationNode computeTransformationNode)
                {
                    foreach (ComputeExpression compute in computeTransformationNode.Expressions)
                        hash = CombineHashCodes(hash, compute.Alias.GetHashCode());
                }
                else
                    throw new InvalidProgramException("Unknown TransformationNode " + transformationNode.GetType().ToString());
            }

            return hash;
        }
        private static int GetCacheCode(int hash, SelectExpandClause selectExpandClause)
        {
            foreach (SelectItem selectItem in selectExpandClause.SelectedItems)
            {
                if (selectItem is ExpandedNavigationSelectItem navigationSelectItem)
                {
                    hash = CombineHashCodes(hash, navigationSelectItem.NavigationSource.Name.GetHashCode());
                    if (navigationSelectItem.SelectAndExpand != null)
                        hash = CombineHashCodes(hash, GetCacheCode(hash, navigationSelectItem.SelectAndExpand));
                }
                else if (selectItem is PathSelectItem pathSelectItem)
                    hash = CombineHashCodes(hash, pathSelectItem.SelectedPath.LastSegment.Identifier.GetHashCode());
                else if (selectItem is OePageSelectItem)
                    hash = CombineHashCodes(hash, typeof(OePageSelectItem).GetHashCode());
                else if (selectItem is OeNextLinkSelectItem)
                    hash = CombineHashCodes(hash, typeof(OeNextLinkSelectItem).GetHashCode());
                else
                    throw new InvalidOperationException("Unknown SelectItem " + selectItem.GetType().ToString());
            }

            return hash;
        }

        public IReadOnlyList<OeQueryCacheDbParameterValue> ParameterValues => _parameterValues.ParameterValues;
    }
}
