using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System;
using System.Linq.Expressions;
using System.Reflection;

namespace OdataToEntity.Parsers.Translators
{
    internal sealed class OeSelectItemTranslator : SelectItemTranslator<OeSelectItem>
    {
        private readonly OeJoinBuilder _joinBuilder;
        private readonly OeSelectItem _navigationItem;
        private readonly bool _navigationNextLink;
        private readonly bool _skipToken;
        private Expression _source;
        private readonly OeQueryNodeVisitor _visitor;

        public OeSelectItemTranslator(OeSelectItem navigationItem, bool navigationNextLink,
            Expression source, OeJoinBuilder joinBuilder, bool skipToken)
        {
            _navigationItem = navigationItem;
            _navigationNextLink = navigationNextLink;
            _source = source;
            _joinBuilder = joinBuilder;
            _skipToken = skipToken;
            _visitor = joinBuilder.Visitor;
        }

        private Expression GetInnerSource(OeSelectItem navigationItem, ExpandedNavigationSelectItem item)
        {
            Type clrEntityType = _visitor.EdmModel.GetClrType(navigationItem.EdmProperty.DeclaringType);
            PropertyInfo navigationClrProperty = clrEntityType.GetPropertyIgnoreCase(navigationItem.EdmProperty);

            Type itemType = OeExpressionHelper.GetCollectionItemType(navigationClrProperty.PropertyType);
            if (itemType == null)
                itemType = navigationClrProperty.PropertyType;

            var visitor = new OeQueryNodeVisitor(_visitor, Expression.Parameter(itemType));
            var expressionBuilder = new OeExpressionBuilder(_joinBuilder, visitor);

            var navigationEdmProperty = (IEdmNavigationProperty)navigationItem.EdmProperty;
            if (navigationEdmProperty.ContainsTarget)
            {
                ModelBuilder.ManyToManyJoinDescription joinDescription = _visitor.EdmModel.GetManyToManyJoinDescription(navigationEdmProperty);
                navigationEdmProperty = joinDescription.TargetNavigationProperty;
            }
            IEdmEntitySet innerEntitySet = OeEdmClrHelper.GetEntitySet(_visitor.EdmModel, navigationEdmProperty);
            Expression innerSource = OeEnumerableStub.CreateEnumerableStubExpression(itemType, innerEntitySet);

            innerSource = expressionBuilder.ApplyFilter(innerSource, item.FilterOption);
            if (item.SkipOption != null || item.TopOption != null)
            {
                Expression source = OeEnumerableStub.CreateEnumerableStubExpression(navigationClrProperty.DeclaringType, (IEdmEntitySet)_navigationItem.EntitySet);
                innerSource = OeCrossApplyBuilder.Build(source, innerSource, item, navigationItem.Path, expressionBuilder);
            }

            return innerSource;
        }
        public override OeSelectItem Translate(ExpandedNavigationSelectItem item)
        {
            if (_navigationNextLink && Cache.UriCompare.OeComparerExtension.GetNavigationNextLink(item))
                return null;

            IEdmNavigationProperty navigationProperty = (((NavigationPropertySegment)item.PathToNavigationProperty.LastSegment).NavigationProperty);
            OeSelectItem navigationItem = _navigationItem.FindChildrenNavigationItem(navigationProperty);
            if (navigationItem == null)
            {
                navigationItem = new OeSelectItem(_visitor.EdmModel, _navigationItem, item, _skipToken);
                _navigationItem.AddNavigationItem(navigationItem);

                Expression innerSource = GetInnerSource(navigationItem, item);
                _source = _joinBuilder.Build(_source, innerSource, _navigationItem.GetJoinPath(), navigationProperty);
            }

            var selectTranslator = new OeSelectTranslator(_joinBuilder, navigationItem);
            _source = selectTranslator.BuildSelect(item.SelectAndExpand, _source, _navigationNextLink, _skipToken);

            return navigationItem;
        }
        public override OeSelectItem Translate(PathSelectItem item)
        {
            if (item.SelectedPath.LastSegment is NavigationPropertySegment navigationSegment)
            {
                IEdmNavigationSource navigationSource = navigationSegment.NavigationSource;
                if (navigationSource == null)
                    navigationSource = OeEdmClrHelper.GetEntitySet(_visitor.EdmModel, navigationSegment.NavigationProperty);

                var navigationSelectItem = new ExpandedNavigationSelectItem(new ODataExpandPath(item.SelectedPath), navigationSource, new SelectExpandClause(null, true));
                return Translate(navigationSelectItem);
            }

            if (item.SelectedPath.LastSegment is PropertySegment propertySegment)
                return new OeSelectItem(propertySegment.Property, _skipToken);

            throw new InvalidOperationException(item.SelectedPath.LastSegment.GetType().Name + " not supported");
        }

        public Expression Source => _source;
    }
}
