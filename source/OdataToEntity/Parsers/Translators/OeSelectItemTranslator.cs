using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace OdataToEntity.Parsers.Translators
{
    internal sealed class OeSelectItemTranslator : SelectItemTranslator<OeSelectItem>
    {
        private readonly OeGroupJoinExpressionBuilder _groupJoinBuilder;
        private readonly OeMetadataLevel _metadataLevel;
        private readonly OeSelectItem _navigationItem;
        private readonly bool _navigationNextLink;
        private readonly ParameterExpression _parameter;
        private readonly bool _skipToken;
        private Expression _source;
        private readonly OeQueryNodeVisitor _visitor;

        public OeSelectItemTranslator(OeSelectItem navigationItem, OeQueryNodeVisitor visitor, ODataPath path, OeMetadataLevel metadataLevel, bool navigationNextLink,
            ParameterExpression parameter, Expression source, OeGroupJoinExpressionBuilder groupJoinBuilder, bool skipToken)
        {
            _navigationItem = navigationItem;
            _visitor = visitor;
            _metadataLevel = metadataLevel;
            _navigationNextLink = navigationNextLink;
            _parameter = parameter;
            _source = source;
            _groupJoinBuilder = groupJoinBuilder;
            _skipToken = skipToken;
        }

        private OeSelectItem CreateNavigationItem(ExpandedNavigationSelectItem item)
        {
            var segment = (NavigationPropertySegment)item.PathToNavigationProperty.LastSegment;
            IEdmNavigationProperty navigationProperty = segment.NavigationProperty;

            var resourceInfo = new ODataNestedResourceInfo()
            {
                IsCollection = navigationProperty.Type.Definition is EdmCollectionType,
                Name = navigationProperty.Name
            };

            var path = new ODataPath(_navigationItem.Path.Union(item.PathToNavigationProperty));
            IEdmEntitySet entitySet = OeEdmClrHelper.GetEntitySet(_visitor.EdmModel, navigationProperty.ToEntityType());
            return new OeSelectItem(_navigationItem, entitySet, path, navigationProperty, resourceInfo, item.CountOption, _skipToken);
        }
        private static List<IEdmNavigationProperty> GetGroupJoinPaths(OeSelectItem navigationItem)
        {
            var groupJoinPaths = new List<IEdmNavigationProperty>();
            for (; navigationItem.Parent != null; navigationItem = navigationItem.Parent)
                groupJoinPaths.Insert(0, (IEdmNavigationProperty)navigationItem.EdmProperty);
            return groupJoinPaths;
        }
        private Expression GetInnerSource(OeSelectItem navigationItem, ExpandedNavigationSelectItem item)
        {
            PropertyInfo navigationClrProperty = OeEdmClrHelper.GetPropertyIgnoreCase(_parameter.Type, navigationItem.EdmProperty.Name);
            Type itemType = OeExpressionHelper.GetCollectionItemType(navigationClrProperty.PropertyType);
            if (itemType == null)
                itemType = navigationClrProperty.PropertyType;

            var visitor = new OeQueryNodeVisitor(_visitor, Expression.Parameter(itemType));
            var expressionBuilder = new OeExpressionBuilder(_groupJoinBuilder, visitor);

            Expression innerSource = Expression.Constant(null, typeof(IEnumerable<>).MakeGenericType(itemType));
            innerSource = expressionBuilder.ApplyFilter(innerSource, item.FilterOption);
            if (item.SkipOption != null || item.TopOption != null)
            {
                Expression source = Expression.Constant(null, typeof(IEnumerable<>).MakeGenericType(navigationClrProperty.DeclaringType));
                innerSource = OeCrossApplyExpressionBuilder.Build(source, innerSource, item, navigationItem.Path, expressionBuilder);
            }

            return innerSource;
        }
        public override OeSelectItem Translate(ExpandedNavigationSelectItem item)
        {
            var segment = (NavigationPropertySegment)item.PathToNavigationProperty.LastSegment;
            if (_navigationNextLink && segment.NavigationProperty.Type is IEdmCollectionTypeReference)
                return null;

            Type itemType;
            OeSelectItem navigationItem = _navigationItem.FindChildrenNavigationItem(segment.NavigationProperty);
            if (navigationItem == null)
            {
                navigationItem = CreateNavigationItem(item);
                _navigationItem.AddNavigationItem(navigationItem);

                Expression innerSource = GetInnerSource(navigationItem, item);
                _source = _groupJoinBuilder.Build(_source, innerSource, GetGroupJoinPaths(_navigationItem), segment.NavigationProperty);
                navigationItem.Expression = _source;

                itemType = OeExpressionHelper.GetCollectionItemType(innerSource.Type);
            }
            else
                itemType = OeEdmClrHelper.GetClrType(_visitor.EdmModel, navigationItem.EntitySet.EntityType());

            if (item.SelectAndExpand.SelectedItems.Any())
            {
                ParameterExpression parameter = Expression.Parameter(itemType);
                var selectTranslator = new OeSelectTranslator(_visitor, navigationItem);
                _source = selectTranslator.BuildSelect(item.SelectAndExpand, _source, parameter, _metadataLevel, _navigationNextLink, _groupJoinBuilder, _skipToken);
            }

            return navigationItem;
        }
        public override OeSelectItem Translate(PathSelectItem item)
        {
            if (item.SelectedPath.LastSegment is NavigationPropertySegment navigationSegment)
            {
                if (_navigationNextLink && navigationSegment.NavigationProperty.Type.Definition is IEdmCollectionType)
                    return null;

                var navigationSelectItem = new ExpandedNavigationSelectItem(new ODataExpandPath(item.SelectedPath), navigationSegment.NavigationSource, new SelectExpandClause(null, true));
                return Translate(navigationSelectItem);
            }

            if (item.SelectedPath.LastSegment is PropertySegment propertySegment)
            {
                Expression expression;
                PropertyInfo property = _parameter.Type.GetProperty(propertySegment.Property.Name);
                if (property == null)
                    expression = new OePropertyTranslator(_source).Build(_parameter, propertySegment.Property);
                else
                    expression = Expression.MakeMemberAccess(_parameter, property);

                return new OeSelectItem(propertySegment.Property, expression, _skipToken);
            }

            throw new InvalidOperationException(item.SelectedPath.LastSegment.GetType().Name + " not supported");
        }

        public Expression Source => _source;
    }
}
