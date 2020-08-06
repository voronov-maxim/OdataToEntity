using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System.Collections.Generic;

namespace OdataToEntity.Query.Builder
{
    public readonly struct OePageNextLinkSelectItemBuilder
    {
        private readonly OeModelBoundProvider _modelBoundProvider;

        public OePageNextLinkSelectItemBuilder(OeModelBoundProvider modelBoundProvider)
        {
            _modelBoundProvider = modelBoundProvider;
        }

        private static void AddPageNextLinkSelectItems(OeModelBoundSettings? settings, SelectExpandClause selectExpandClause, ref List<SelectItem>? selectItems)
        {
            if (settings != null && (settings.PageSize > 0 || settings.NavigationNextLink))
            {
                if (selectItems == null)
                    selectItems = new List<SelectItem>(selectExpandClause.SelectedItems);

                if (settings.PageSize > 0)
                    selectItems.Add(new Parsers.Translators.OePageSelectItem(settings.PageSize));

                if (settings.NavigationNextLink)
                    selectItems.Add(Parsers.Translators.OeNextLinkSelectItem.Instance);
            }
        }
        public SelectExpandClause? Build(SelectExpandClause selectExpandClause, IEdmEntityType entityType)
        {
            return selectExpandClause == null ? null : GetSelectItems(selectExpandClause, _modelBoundProvider.GetSettings(entityType));
        }
        private ExpandedNavigationSelectItem Build(ExpandedNavigationSelectItem navigationSelectItem)
        {
            if (navigationSelectItem.SelectAndExpand == null)
                return navigationSelectItem;

            var segment = (NavigationPropertySegment)navigationSelectItem.PathToNavigationProperty.LastSegment;
            IEdmNavigationProperty navigationProperty = segment.NavigationProperty;

            SelectExpandClause selectExpandClause = GetSelectItems(navigationSelectItem.SelectAndExpand, _modelBoundProvider.GetSettings(navigationProperty));
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
        private SelectExpandClause GetSelectItems(SelectExpandClause selectExpandClause, OeModelBoundSettings? settings)
        {
            List<SelectItem>? selectItems = null;
            int i = 0;
            foreach (SelectItem selectItem in selectExpandClause.SelectedItems)
            {
                if (selectItem is ExpandedNavigationSelectItem navigationSelectItem)
                {
                    ExpandedNavigationSelectItem item = Build(navigationSelectItem);
                    if (item != navigationSelectItem)
                    {
                        if (selectItems == null)
                            selectItems = new List<SelectItem>(selectExpandClause.SelectedItems);
                        selectItems[i] = item;
                    }
                }
                i++;
            }

            AddPageNextLinkSelectItems(settings, selectExpandClause, ref selectItems);
            return selectItems == null ? selectExpandClause : new SelectExpandClause(selectItems, selectExpandClause.AllSelected);
        }
    }
}
