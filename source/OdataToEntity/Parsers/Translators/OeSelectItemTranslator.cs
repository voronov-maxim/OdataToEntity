using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System;

namespace OdataToEntity.Parsers.Translators
{
    internal readonly struct OeSelectItemTranslator
    {
        private readonly IEdmModel _edmModel;
        private readonly OeJoinBuilder _joinBuilder;
        private readonly bool _navigationNextLink;
        private readonly bool _skipToken;

        public OeSelectItemTranslator(OeJoinBuilder joinBuilder, bool navigationNextLink, bool skipToken)
        {
            _navigationNextLink = navigationNextLink;
            _joinBuilder = joinBuilder;
            _skipToken = skipToken;
            _edmModel = joinBuilder.Visitor.EdmModel;
        }

        public void Translate(OeSelectItem navigationItem, SelectItem item)
        {
            if (item is ExpandedNavigationSelectItem expandedNavigationSelectItem)
                Translate(navigationItem, expandedNavigationSelectItem);
            else if (item is PathSelectItem pathSelectItem)
                Translate(navigationItem, pathSelectItem);
            else
                throw new InvalidOperationException("Unknown SelectItem type " + item.GetType().Name);
        }
        private void Translate(OeSelectItem navigationItem, ExpandedNavigationSelectItem item)
        {
            if (_navigationNextLink && Cache.UriCompare.OeComparerExtension.IsNavigationNextLink(item))
                return;

            var childNavigationItem = new OeSelectItem(_edmModel, navigationItem, item, _skipToken);
            childNavigationItem = navigationItem.AddOrGetNavigationItem(childNavigationItem);

            foreach (SelectItem selectItemClause in item.SelectAndExpand.SelectedItems)
                Translate(childNavigationItem, selectItemClause);
        }
        private void Translate(OeSelectItem navigationItem, PathSelectItem item)
        {
            if (item.SelectedPath.LastSegment is NavigationPropertySegment navigationSegment)
            {
                IEdmNavigationSource navigationSource = navigationSegment.NavigationSource;
                if (navigationSource == null)
                    navigationSource = OeEdmClrHelper.GetEntitySet(_edmModel, navigationSegment.NavigationProperty);

                var selectItemClause = new ExpandedNavigationSelectItem(new ODataExpandPath(item.SelectedPath), navigationSource, new SelectExpandClause(null, true));
                Translate(navigationItem, selectItemClause);
            }
            else if (item.SelectedPath.LastSegment is PropertySegment propertySegment)
            {
                var selectItem = new OeSelectItem(propertySegment.Property, _skipToken);
                navigationItem.AddSelectItem(selectItem);
            }
            else
                throw new InvalidOperationException(item.SelectedPath.LastSegment.GetType().Name + " not supported");
        }
    }
}
