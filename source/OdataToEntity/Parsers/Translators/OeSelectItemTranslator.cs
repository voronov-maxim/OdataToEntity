using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System;

namespace OdataToEntity.Parsers.Translators
{
    internal readonly struct OeSelectItemTranslator
    {
        private readonly IEdmModel _edmModel;
        private readonly bool _skipToken;

        public OeSelectItemTranslator(IEdmModel edmModel, bool skipToken)
        {
            _edmModel = edmModel;
            _skipToken = skipToken;
        }

        public void Translate(OeNavigationSelectItem parentNavigationItem, SelectItem item)
        {
            if (item is ExpandedNavigationSelectItem expandedNavigationSelectItem)
                Translate(parentNavigationItem, expandedNavigationSelectItem);
            else if (item is PathSelectItem pathSelectItem)
                Translate(parentNavigationItem, pathSelectItem);
            else if (item is OePageSelectItem pageSelectItem)
                Translate(parentNavigationItem, pageSelectItem);
            else
                throw new InvalidOperationException("Unknown SelectItem type " + item.GetType().Name);
        }
        private void Translate(OeNavigationSelectItem parentNavigationItem, OePageSelectItem pageSelectItem)
        {
            if (parentNavigationItem.NavigationSelectItem != null)
            {
                var segment = (NavigationPropertySegment)parentNavigationItem.NavigationSelectItem.PathToNavigationProperty.LastSegment;
                if (!segment.NavigationProperty.Type.IsCollection())
                    return;
            }

            parentNavigationItem.PageSize = pageSelectItem.PageSize;
        }
        private void Translate(OeNavigationSelectItem parentNavigationItem, ExpandedNavigationSelectItem item)
        {
            if (item.SelectAndExpand.IsNavigationNextLink())
                return;

            IEdmEntitySetBase entitySet = OeEdmClrHelper.GetEntitySet(_edmModel, item);
            var childNavigationSelectItem = new OeNavigationSelectItem(entitySet, parentNavigationItem, item, _skipToken);
            childNavigationSelectItem = parentNavigationItem.AddOrGetNavigationItem(childNavigationSelectItem);

            foreach (SelectItem selectItemClause in item.SelectAndExpand.SelectedItems)
                Translate(childNavigationSelectItem, selectItemClause);
        }
        private void Translate(OeNavigationSelectItem parentNavigationItem, PathSelectItem item)
        {
            if (item.SelectedPath.LastSegment is NavigationPropertySegment navigationSegment)
            {
                IEdmNavigationSource navigationSource = navigationSegment.NavigationSource;
                if (navigationSource == null)
                    navigationSource = OeEdmClrHelper.GetEntitySet(_edmModel, navigationSegment.NavigationProperty);

                var selectItemClause = new ExpandedNavigationSelectItem(new ODataExpandPath(item.SelectedPath), navigationSource, new SelectExpandClause(null, true));
                Translate(parentNavigationItem, selectItemClause);
            }
            else if (item.SelectedPath.LastSegment is PropertySegment propertySegment)
                parentNavigationItem.AddStructuralItem(propertySegment.Property, _skipToken);
            else
                throw new InvalidOperationException(item.SelectedPath.LastSegment.GetType().Name + " not supported");
        }
    }
}
