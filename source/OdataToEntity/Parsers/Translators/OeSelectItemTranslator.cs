using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System;

namespace OdataToEntity.Parsers.Translators
{
    internal readonly struct OeSelectItemTranslator
    {
        private readonly IEdmModel _edmModel;
        private readonly bool _navigationNextLink;
        private readonly bool _skipToken;

        public OeSelectItemTranslator(IEdmModel edmModel, bool navigationNextLink, bool skipToken)
        {
            _edmModel = edmModel;
            _navigationNextLink = navigationNextLink;
            _skipToken = skipToken;
        }

        public void Translate(OeNavigationSelectItem navigationItem, SelectItem item)
        {
            if (item is ExpandedNavigationSelectItem expandedNavigationSelectItem)
                Translate(navigationItem, expandedNavigationSelectItem);
            else if (item is PathSelectItem pathSelectItem)
                Translate(navigationItem, pathSelectItem);
            else if (item is OeDisableSelectItem disableSelectItem)
                Translate(navigationItem, disableSelectItem);
            else
                throw new InvalidOperationException("Unknown SelectItem type " + item.GetType().Name);
        }
        private void Translate(OeNavigationSelectItem navigationItem, OeDisableSelectItem item)
        {
            navigationItem.RemoveStructuralItem(item.StructuralProperty);
        }
        private void Translate(OeNavigationSelectItem navigationItem, ExpandedNavigationSelectItem item)
        {
            if (_navigationNextLink && Cache.UriCompare.OeComparerExtension.IsNavigationNextLink(item))
                return;

            var childNavigationItem = new OeNavigationSelectItem(_edmModel, navigationItem, item, _skipToken);
            childNavigationItem = navigationItem.AddOrGetNavigationItem(childNavigationItem);

            foreach (SelectItem selectItemClause in item.SelectAndExpand.SelectedItems)
                Translate(childNavigationItem, selectItemClause);
        }
        private void Translate(OeNavigationSelectItem navigationItem, PathSelectItem item)
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
                navigationItem.AddStructuralItem(propertySegment.Property, _skipToken);
            else
                throw new InvalidOperationException(item.SelectedPath.LastSegment.GetType().Name + " not supported");
        }
    }
}
