using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System;

namespace OdataToEntity.Parsers.Translators
{
    internal readonly struct OeSelectItemTranslator
    {
        private readonly IEdmModel _edmModel;
        private readonly bool _notSelected;

        public OeSelectItemTranslator(IEdmModel edmModel, bool notSelected)
        {
            _edmModel = edmModel;
            _notSelected = notSelected;
        }

        private OeNavigationSelectItem AddOrGetNavigationItem(OeNavigationSelectItem parentNavigationItem, ExpandedNavigationSelectItem item, bool isExpand)
        {
            IEdmEntitySetBase entitySet = OeEdmClrHelper.GetEntitySet(_edmModel, item);

            OeNavigationSelectItemKind kind;
            if (_notSelected)
                kind = OeNavigationSelectItemKind.NotSelected;
            else if (item.SelectAndExpand.IsNextLink())
                kind = OeNavigationSelectItemKind.NextLink;
            else
                kind = OeNavigationSelectItemKind.Normal;

            var childNavigationSelectItem = new OeNavigationSelectItem(entitySet, parentNavigationItem, item, kind);
            return parentNavigationItem.AddOrGetNavigationItem(childNavigationSelectItem, isExpand);
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
        private static void Translate(OeNavigationSelectItem parentNavigationItem, OePageSelectItem pageSelectItem)
        {
            if (parentNavigationItem.Parent != null)
            {
                var segment = (NavigationPropertySegment)parentNavigationItem.NavigationSelectItem.PathToNavigationProperty.LastSegment;
                if (!segment.NavigationProperty.Type.IsCollection())
                    return;
            }

            parentNavigationItem.PageSize = pageSelectItem.PageSize;
        }
        private void Translate(OeNavigationSelectItem parentNavigationItem, ExpandedNavigationSelectItem item)
        {
            OeNavigationSelectItem childNavigationSelectItem = AddOrGetNavigationItem(parentNavigationItem, item, true);
            if (childNavigationSelectItem.Kind == OeNavigationSelectItemKind.NextLink)
                return;

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

                var expandedItem = new ExpandedNavigationSelectItem(new ODataExpandPath(item.SelectedPath), navigationSource, new SelectExpandClause(null, true));
                AddOrGetNavigationItem(parentNavigationItem, expandedItem, false);
            }
            else if (item.SelectedPath.LastSegment is PropertySegment propertySegment)
                parentNavigationItem.AddStructuralItem(propertySegment.Property, _notSelected);
            else
                throw new InvalidOperationException(item.SelectedPath.LastSegment.GetType().Name + " not supported");
        }
    }
}
