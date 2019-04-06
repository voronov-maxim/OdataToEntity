using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using OdataToEntity.Parsers.Translators;
using System;
using System.Collections.Generic;

namespace OdataToEntity.Query.Builder
{
    public readonly struct OeSelectExpandBuilder
    {
        private readonly IEdmModel _edmModel;
        private readonly OeModelBoundSettingsBuilder _modelBoundSettingsBuilder;
        private readonly Dictionary<(IEdmEntityType, int), List<SelectItem>> _selectItemsCache;
        private readonly HashSet<IEdmNavigationProperty> _visited;

        public OeSelectExpandBuilder(IEdmModel edmModel, OeModelBoundSettingsBuilder modelBoundSettingsBuilder)
        {
            _edmModel = edmModel;
            _modelBoundSettingsBuilder = modelBoundSettingsBuilder;

            _selectItemsCache = new Dictionary<(IEdmEntityType, int), List<SelectItem>>();
            _visited = new HashSet<IEdmNavigationProperty>();
        }

        public SelectItem[] Build(IEdmEntityType entityType, int level)
        {
            _visited.Clear();
            List<SelectItem> selectItems = BuildSelectItems(entityType, level);
            return selectItems.Count == 0 ? Array.Empty<SelectItem>() : selectItems.ToArray();
        }
        private ExpandedNavigationSelectItem BuildNavigation(IEdmNavigationProperty navigationProperty, int level)
        {
            List<SelectItem> selectItems = GetNavigationSelectItems(navigationProperty, level);

            IEdmEntitySet entitySet = OeEdmClrHelper.GetEntitySet(_edmModel, navigationProperty);
            var expandPath = new ODataExpandPath(new NavigationPropertySegment(navigationProperty, entitySet));

            OeModelBoundSettings navigationSettings = _modelBoundSettingsBuilder.GetSettings(navigationProperty);
            if (navigationSettings == null)
                return new ExpandedNavigationSelectItem(expandPath, entitySet, new SelectExpandClause(selectItems, false));

            MergeSelectItems<IEdmStructuralProperty, PathSelectItem>(selectItems, navigationSettings, level);
            MergeSelectItems<IEdmNavigationProperty, ExpandedNavigationSelectItem>(selectItems, navigationSettings, level);

            return new ExpandedNavigationSelectItem(expandPath, entitySet, new SelectExpandClause(selectItems, false));
        }
        private List<SelectItem> BuildSelectItems(IEdmEntityType entityType, int level)
        {
            if (level < 0)
                return new List<SelectItem>();

            if (_selectItemsCache.TryGetValue((entityType, level), out List<SelectItem> selectItems))
                return selectItems;

            selectItems = new List<SelectItem>();
            foreach (IEdmProperty property in entityType.Properties())
            {
                OeModelBoundSettings settings = _modelBoundSettingsBuilder.GetSettings((IEdmEntityType)property.DeclaringType);
                if (settings != null)
                {
                    SelectExpandType? propertySetting = settings.GetPropertySetting(property, OeModelBoundKind.Select);
                    if (property is IEdmNavigationProperty navigationProperty && level > 0)
                    {
                        if (propertySetting == SelectExpandType.Automatic)
                            selectItems.Add(BuildNavigation(navigationProperty, level));
                    }
                    else if (property is IEdmStructuralProperty structuralProperty)
                    {
                        if (propertySetting == SelectExpandType.Automatic)
                            selectItems.Add(new PathSelectItem(new ODataSelectPath(new PropertySegment(structuralProperty))));
                        else if (propertySetting == SelectExpandType.Disabled)
                            selectItems.Add(new OeDisableSelectItem(structuralProperty));
                    }
                }
            }

            _selectItemsCache.Add((entityType, level), selectItems);
            return selectItems;
        }
        private SelectItem CreateSelectItem<TProperty>(TProperty property, int level) where TProperty : IEdmProperty
        {
            if (property is IEdmStructuralProperty structuralProperty)
                return new PathSelectItem(new ODataSelectPath(new PropertySegment(structuralProperty)));
            else if (property is IEdmNavigationProperty navigationProperty)
            {
                IEdmEntitySet entitySet = OeEdmClrHelper.GetEntitySet(_edmModel, navigationProperty);
                var navigationSegment = new NavigationPropertySegment(navigationProperty, entitySet);
                var expandPath = new ODataExpandPath(navigationSegment);

                IList<SelectItem> selectItems = GetNavigationSelectItems(navigationProperty, level);
                return new ExpandedNavigationSelectItem(expandPath, entitySet, new SelectExpandClause(selectItems, false));
            }
            else
                throw new InvalidOperationException("Unsupported IEdmPropertyType type " + property.GetType().Name);
        }
        private List<SelectItem> GetNavigationSelectItems(IEdmNavigationProperty navigationProperty, int level)
        {
            var selectItems = new List<SelectItem>();
            if (_visited.Add(navigationProperty))
            {
                selectItems = BuildSelectItems(navigationProperty.ToEntityType(), level - 1);
                _visited.Remove(navigationProperty);
            }
            else
            {
                if (level >= 0)
                    selectItems = BuildSelectItems(navigationProperty.ToEntityType(), 0);
            }
            return selectItems;
        }
        private TProperty GetProperty<TProperty, TSelectItem>(TSelectItem selectItem)
            where TSelectItem : SelectItem
            where TProperty : IEdmProperty
        {
            if (selectItem is PathSelectItem pathSelectItem)
            {
                var segment = (PropertySegment)pathSelectItem.SelectedPath.LastSegment;
                return (TProperty)segment.Property;
            }
            else if (selectItem is ExpandedNavigationSelectItem navigationSelectItem)
            {
                var segment = (NavigationPropertyLinkSegment)navigationSelectItem.PathToNavigationProperty.LastSegment;
                return (TProperty)segment.NavigationProperty;
            }
            else
                throw new InvalidOperationException("Unsupported SelectItem type " + selectItem.GetType().Name);
        }
        private void MergeSelectItems<TProperty, TSelectItem>(List<SelectItem> selectItems, OeModelBoundSettings navigationSettings, int level)
            where TSelectItem : SelectItem
            where TProperty : IEdmProperty
        {
            var automaticProperties = new List<TProperty>();
            var disabledProperties = new HashSet<TProperty>();

            foreach ((IEdmProperty, SelectExpandType) propertySetting in navigationSettings.GetPropertySettings(OeModelBoundKind.Select))
                if (propertySetting.Item1 is TProperty property)
                    if (propertySetting.Item2 == SelectExpandType.Automatic)
                        automaticProperties.Add(property);
                    else if (propertySetting.Item2 == SelectExpandType.Disabled)
                        disabledProperties.Add(property);

            if (automaticProperties.Count > 0 || disabledProperties.Count > 0)
            {
                if (automaticProperties.Count > 0)
                {
                    for (int i = selectItems.Count - 1; i >= 0; i--)
                        if (selectItems[i] is TSelectItem)
                            selectItems.RemoveAt(i);

                    for (int i = 0; i < automaticProperties.Count; i++)
                        selectItems.Add(CreateSelectItem(automaticProperties[i], level));
                }
                else
                {
                    for (int i = selectItems.Count - 1; i >= 0; i--)
                        if (selectItems[i] is TSelectItem selectItem && disabledProperties.Contains(GetProperty<TProperty, TSelectItem>(selectItem)))
                            selectItems.RemoveAt(i);
                }
            }
        }
    }
}
