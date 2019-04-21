using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System.Collections.Generic;

namespace OdataToEntity.Test
{
    public readonly struct PageSelectItemBuilder
    {
        private readonly bool _navigationNextLink;
        private readonly int _pageSize;

        public PageSelectItemBuilder(bool navigationNextLink, int pageSize)
        {
            _navigationNextLink = navigationNextLink;
            _pageSize = pageSize;
        }

        public SelectExpandClause Build(SelectExpandClause selectExpandClause)
        {
            return selectExpandClause == null ? null : GetSelectItems(selectExpandClause, null);
        }
        private ExpandedNavigationSelectItem Build(ExpandedNavigationSelectItem navigationSelectItem)
        {
            if (navigationSelectItem.SelectAndExpand == null)
                return navigationSelectItem;

            SelectExpandClause selectExpandClause = GetSelectItems(navigationSelectItem.SelectAndExpand, navigationSelectItem);
            if (selectExpandClause == navigationSelectItem.SelectAndExpand)
                return navigationSelectItem;

            return new ExpandedNavigationSelectItem(
                navigationSelectItem.PathToNavigationProperty,
                navigationSelectItem.NavigationSource,
                selectExpandClause,
                navigationSelectItem.FilterOption,
                navigationSelectItem.OrderByOption,
                navigationSelectItem.TopOption,
                navigationSelectItem.SkipOption,
                navigationSelectItem.CountOption,
                navigationSelectItem.SearchOption,
                navigationSelectItem.LevelsOption);
        }
        private IEdmNavigationProperty GetNavigationProperty(ExpandedNavigationSelectItem navigationSelectItem)
        {
            return ((NavigationPropertySegment)navigationSelectItem.PathToNavigationProperty.LastSegment).NavigationProperty;
        }
        private SelectExpandClause GetSelectItems(SelectExpandClause selectExpandClause, ExpandedNavigationSelectItem navigationSelectItem)
        {
            var selectItems = new List<SelectItem>(selectExpandClause.SelectedItems);
            for (int i = 0; i < selectItems.Count; i++)
                if (selectItems[i] is ExpandedNavigationSelectItem item)
                    selectItems[i] = Build(item);

            if (navigationSelectItem == null || GetNavigationProperty(navigationSelectItem).Type.Definition is EdmCollectionType)
                selectItems.Add(new Parsers.Translators.OePageSelectItem(_pageSize, _navigationNextLink));
            return new SelectExpandClause(selectItems, selectExpandClause.AllSelected);
        }
    }
}
