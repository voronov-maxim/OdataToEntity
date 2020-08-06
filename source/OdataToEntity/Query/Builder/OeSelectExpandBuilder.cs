using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using OdataToEntity.Parsers.Translators;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace OdataToEntity.Query.Builder
{
    public readonly struct OeSelectExpandBuilder
    {
        private readonly IEdmModel _edmModel;
        private readonly OeModelBoundProvider _modelBoundProvider;
        private static readonly ConcurrentDictionary<(IEdmNavigationProperty, int), List<SelectItem>> _selectItemsCache = new ConcurrentDictionary<(IEdmNavigationProperty, int), List<SelectItem>>();
        private readonly HashSet<IEdmNavigationProperty> _visited;

        public OeSelectExpandBuilder(IEdmModel edmModel, OeModelBoundProvider modelBoundProvider)
        {
            _edmModel = edmModel;
            _modelBoundProvider = modelBoundProvider;

            _visited = new HashSet<IEdmNavigationProperty>();
        }

        private static void AddPageNextLinkSelectItems(OeModelBoundSettings settings, List<SelectItem> selectItems)
        {
            if (settings.PageSize > 0)
            {
                int index = selectItems.FindIndex(i => i is OePageSelectItem);
                if (settings.NavigationProperty == null)
                {
                    if (index < 0)
                        selectItems.Add(new OePageSelectItem(settings.PageSize));
                }
                else
                {
                    var pageSelectItem = new OePageSelectItem(settings.PageSize);
                    if (index >= 0)
                        selectItems[index] = pageSelectItem;
                    else
                        selectItems.Add(pageSelectItem);
                }
            }
            else if (settings.PageSize < 0 && settings.NavigationProperty != null)
            {
                int index = selectItems.FindIndex(i => i is OePageSelectItem);
                if (index >= 0)
                    selectItems.RemoveAt(index);
            }

            if (settings.NavigationNextLink)
                selectItems.Add(OeNextLinkSelectItem.Instance);
        }
        public SelectExpandClause Build(SelectExpandClause selectExpandClause, IEdmEntityType entityType)
        {
            _visited.Clear();

            OeModelBoundSettings? settings = _modelBoundProvider.GetSettings(entityType);

            var selectItems = new List<SelectItem>();
            var properties = new HashSet<IEdmProperty>();
            if (selectExpandClause != null)
                foreach (SelectItem selectItem in selectExpandClause.SelectedItems)
                    if (selectItem is ExpandedNavigationSelectItem navigationSelectITem)
                    {
                        var segment = (NavigationPropertySegment)navigationSelectITem.PathToNavigationProperty.LastSegment;
                        if (settings != null && settings.GetPropertySetting(segment.NavigationProperty, OeModelBoundKind.Select) == SelectExpandType.Disabled)
                            continue;

                        selectItems.Add(CreateNavigationSelectItem(navigationSelectITem));
                        properties.Add(segment.NavigationProperty);
                    }
                    else if (selectItem is PathSelectItem pathSelectItem)
                    {
                        IEdmProperty property;
                        if (pathSelectItem.SelectedPath.LastSegment is PropertySegment propertySegment)
                            property = propertySegment.Property;
                        else if (pathSelectItem.SelectedPath.LastSegment is NavigationPropertySegment navigationSegment)
                            property = navigationSegment.NavigationProperty;
                        else
                            throw new InvalidOperationException("Unknown segment " + pathSelectItem.SelectedPath.LastSegment.GetType().Name);

                        if (settings == null || settings.GetPropertySetting(property, OeModelBoundKind.Select) != SelectExpandType.Disabled)
                        {
                            selectItems.Add(pathSelectItem);
                            properties.Add(property);
                        }
                    }

            if (settings != null)
            {
                foreach ((IEdmProperty, SelectExpandType) propertySetting in settings.GetPropertySettings(OeModelBoundKind.Select))
                    if (propertySetting.Item2 == SelectExpandType.Automatic && !properties.Contains(propertySetting.Item1))
                    {
                        if (propertySetting.Item1 is IEdmNavigationProperty navigationProperty)
                            selectItems.Add(CreateNavigationSelectItem(navigationProperty, 3));
                        else if (propertySetting.Item1 is IEdmStructuralProperty structuralProperty)
                            selectItems.Add(CreateStructuralSelectItem(structuralProperty));
                    }

                AddPageNextLinkSelectItems(settings, selectItems);
            }

            return new SelectExpandClause(selectItems, selectItems.Count == 0);
        }
        private ExpandedNavigationSelectItem CreateNavigationSelectItem(ExpandedNavigationSelectItem navigationSelectItem)
        {
            var segment = (NavigationPropertySegment)navigationSelectItem.PathToNavigationProperty.LastSegment;
            IEdmNavigationProperty navigationProperty = segment.NavigationProperty;

            bool allSelected = false;
            List<SelectItem> selectItems;
            if (navigationSelectItem.SelectAndExpand == null)
                selectItems = GetSelectItems(navigationProperty, 3);
            else
            {
                IEdmEntityType entityType = navigationProperty.ToEntityType();

                int propertiesCount = 0;
                selectItems = new List<SelectItem>(navigationSelectItem.SelectAndExpand.SelectedItems);
                if (selectItems.Count == 0 && navigationSelectItem.SelectAndExpand.AllSelected)
                {
                    foreach (IEdmStructuralProperty structuralProperty in entityType.StructuralProperties())
                        selectItems.Add(CreateStructuralSelectItem(structuralProperty));
                    propertiesCount = selectItems.Count;
                }

                OeModelBoundSettings? settings = _modelBoundProvider.GetSettings(entityType);
                if (settings != null)
                    MergeSelectItems(selectItems, settings, 3);

                settings = _modelBoundProvider.GetSettings(navigationProperty);
                if (settings != null)
                    MergeSelectItems(selectItems, settings, 3);

                if (propertiesCount > 0 && propertiesCount <= selectItems.Count)
                {
                    int i = 0;
                    while (i < propertiesCount && selectItems[i] is PathSelectItem)
                        i++;

                    if (i == selectItems.Count)
                        return navigationSelectItem;

                    if (i < selectItems.Count)
                    {
                        int j = i;
                        while (j < selectItems.Count && (selectItems[j] is OePageSelectItem || selectItems[j] is OeNextLinkSelectItem))
                            j++;

                        if (j == selectItems.Count)
                        {
                            var temp = new List<SelectItem>();
                            for (; i < selectItems.Count; i++)
                                temp.Add(selectItems[i]);
                            selectItems = temp;
                            allSelected = true;
                        }
                    }
                }
            }

            return new ExpandedNavigationSelectItem(
                navigationSelectItem.PathToNavigationProperty,
                navigationSelectItem.NavigationSource,
                new SelectExpandClause(selectItems, selectItems.Count == 0 || allSelected),
                navigationSelectItem.FilterOption,
                navigationSelectItem.OrderByOption,
                navigationSelectItem.TopOption,
                navigationSelectItem.SkipOption,
                navigationSelectItem.CountOption,
                navigationSelectItem.SearchOption,
                navigationSelectItem.LevelsOption);
        }
        private ExpandedNavigationSelectItem CreateNavigationSelectItem(IEdmNavigationProperty navigationProperty, int level)
        {
            IEdmEntitySet entitySet = OeEdmClrHelper.GetEntitySet(_edmModel, navigationProperty);
            var expandPath = new ODataExpandPath(new NavigationPropertySegment(navigationProperty, entitySet));

            List<SelectItem> selectItems = GetSelectItems(navigationProperty, level);
            var selectExpandClause = new SelectExpandClause(selectItems, selectItems.Count == 0);
            return new ExpandedNavigationSelectItem(expandPath, entitySet, selectExpandClause);
        }
        private static IEdmProperty GetProperty(SelectItem selectItem)
        {
            if (selectItem is PathSelectItem pathSelectItem)
            {
                var segment = (PropertySegment)pathSelectItem.SelectedPath.LastSegment;
                return segment.Property;
            }

            if (selectItem is ExpandedNavigationSelectItem navigationSelectItem)
            {
                var segment = (NavigationPropertySegment)navigationSelectItem.PathToNavigationProperty.LastSegment;
                return segment.NavigationProperty;
            }

            throw new InvalidOperationException("Unsupported SelectItem type " + selectItem.GetType().Name);
        }
        private static PathSelectItem CreateStructuralSelectItem(IEdmStructuralProperty structuralProperty)
        {
            return new PathSelectItem(new ODataSelectPath(new PropertySegment(structuralProperty)));
        }
        private List<SelectItem> GetSelectItems(IEdmEntityType entityType, int level)
        {
            var selectItems = new List<SelectItem>();
            if (level < 0)
                return selectItems;

            bool disabledMatched = false;
            foreach (IEdmProperty property in entityType.Properties())
            {
                OeModelBoundSettings? settings = _modelBoundProvider.GetSettings((IEdmEntityType)property.DeclaringType);
                if (settings != null)
                {
                    SelectExpandType? propertySetting = settings.GetPropertySetting(property, OeModelBoundKind.Select);
                    if (property is IEdmNavigationProperty navigationProperty && level > 0)
                    {
                        if (propertySetting == SelectExpandType.Automatic)
                            selectItems.Add(CreateNavigationSelectItem(navigationProperty, level));
                    }
                    else if (property is IEdmStructuralProperty structuralProperty)
                    {
                        if (propertySetting == SelectExpandType.Automatic)
                            selectItems.Add(CreateStructuralSelectItem(structuralProperty));
                        else if (propertySetting == SelectExpandType.Disabled)
                            disabledMatched = true;
                    }
                }
            }

            if (selectItems.Count == 0 && disabledMatched)
                foreach (IEdmStructuralProperty structuralProperty in entityType.StructuralProperties())
                {
                    OeModelBoundSettings? settings = _modelBoundProvider.GetSettings((IEdmEntityType)structuralProperty.DeclaringType);
                    if (settings == null || settings.GetPropertySetting(structuralProperty, OeModelBoundKind.Select) != SelectExpandType.Disabled)
                        selectItems.Add(CreateStructuralSelectItem(structuralProperty));
                }

            return selectItems;
        }
        private List<SelectItem> GetSelectItems(IEdmNavigationProperty navigationProperty, int level)
        {
            if (_selectItemsCache.TryGetValue((navigationProperty, level), out List<SelectItem>? selectItems))
                return selectItems;

            if (_visited.Add(navigationProperty))
            {
                selectItems = GetSelectItems(navigationProperty.ToEntityType(), level - 1);
                _visited.Remove(navigationProperty);
            }
            else
            {
                if (level >= 0)
                    selectItems = GetSelectItems(navigationProperty.ToEntityType(), 0);
                else
                    selectItems = new List<SelectItem>();
            }

            OeModelBoundSettings? navigationSettings = _modelBoundProvider.GetSettings(navigationProperty);
            if (navigationSettings != null)
                MergeSelectItems(selectItems, navigationSettings, level);

            _selectItemsCache.TryAdd((navigationProperty, level), selectItems);
            return selectItems;
        }
        private void MergeSelectItems(List<SelectItem> selectItems, OeModelBoundSettings settings, int level)
        {
            var automaticProperties = new List<IEdmProperty>();
            var disabledProperties = new HashSet<IEdmProperty>();

            foreach ((IEdmProperty, SelectExpandType) propertySetting in settings.GetPropertySettings(OeModelBoundKind.Select))
                if (propertySetting.Item2 == SelectExpandType.Automatic)
                    automaticProperties.Add(propertySetting.Item1);
                else if (propertySetting.Item2 == SelectExpandType.Disabled)
                    disabledProperties.Add(propertySetting.Item1);

            if (automaticProperties.Count > 0)
            {
                var propertyIndex = new Dictionary<IEdmProperty, int>(selectItems.Count);
                for (int i = 0; i < selectItems.Count; i++)
                    propertyIndex.Add(GetProperty(selectItems[i]), i);

                for (int i = 0; i < automaticProperties.Count; i++)
                    if (automaticProperties[i] is IEdmNavigationProperty navigationProperty)
                    {
                        SelectItem navigationSelectItem = CreateNavigationSelectItem(navigationProperty, level);
                        if (propertyIndex.TryGetValue(navigationProperty, out int index))
                            selectItems[index] = navigationSelectItem;
                        else
                            selectItems.Add(navigationSelectItem);
                    }
                    else if (automaticProperties[i] is IEdmStructuralProperty structuralProperty)
                    {
                        if (!propertyIndex.ContainsKey(structuralProperty))
                            selectItems.Add(CreateStructuralSelectItem(structuralProperty));
                    }
                    else
                        throw new InvalidOperationException("Unsupported IEdmProperty type " + automaticProperties[i].GetType().Name);
            }
            else if (disabledProperties.Count > 0)
            {
                for (int i = selectItems.Count - 1; i >= 0; i--)
                    if (disabledProperties.Contains(GetProperty(selectItems[i])))
                        selectItems.RemoveAt(i);
            }

            AddPageNextLinkSelectItems(settings, selectItems);
        }
    }
}
