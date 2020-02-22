using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System;
using System.Collections.Generic;

namespace OdataToEntity.Parsers.Translators
{
    internal enum OeNavigationSelectItemKind
    {
        Normal,
        NotSelected,
        NextLink
    }

    internal sealed class OeNavigationSelectItem
    {
        private bool _allSelected;
        private readonly IEdmNavigationProperty? _edmProperty;
        private OeEntryFactory? _entryFactory;
        private readonly List<OeNavigationSelectItem> _navigationItems;
        private ExpandedNavigationSelectItem? _navigationSelectItem;
        private readonly List<OeStructuralSelectItem> _structuralItems;
        private readonly List<OeStructuralSelectItem> _structuralItemsNotSelected;

        private OeNavigationSelectItem(IEdmEntitySetBase entitySet, ODataPath path)
        {
            EntitySet = entitySet;
            Path = path;

            _navigationItems = new List<OeNavigationSelectItem>();
            _structuralItems = new List<OeStructuralSelectItem>();
            _structuralItemsNotSelected = new List<OeStructuralSelectItem>();
            _allSelected = true;
        }
        public OeNavigationSelectItem(ODataUri odataUri)
            : this(GetEntitySet(odataUri.Path), odataUri.Path)
        {
            Kind = OeNavigationSelectItemKind.Normal;
        }
        public OeNavigationSelectItem(IEdmEntitySetBase entitySet, OeNavigationSelectItem parent, ExpandedNavigationSelectItem item, OeNavigationSelectItemKind kind)
            : this(entitySet, CreatePath(parent.Path, item.PathToNavigationProperty))
        {
            _edmProperty = ((NavigationPropertySegment)item.PathToNavigationProperty.LastSegment).NavigationProperty;
            _navigationSelectItem = item;
            EntitySet = entitySet;
            Kind = kind;
            Parent = parent;
        }

        internal void AddKeyRecursive(bool notSelected)
        {
            if (!AllSelected)
                foreach (IEdmStructuralProperty keyProperty in EntitySet.EntityType().Key())
                    AddStructuralItem(keyProperty, notSelected);

            if (_navigationSelectItem != null)
            {
                OrderByClause orderByClause = OeSkipTokenParser.GetUniqueOrderBy(EntitySet, _navigationSelectItem.OrderByOption, null);
                if (_navigationSelectItem.OrderByOption != orderByClause)
                    _navigationSelectItem = new ExpandedNavigationSelectItem(
                        _navigationSelectItem.PathToNavigationProperty,
                        _navigationSelectItem.NavigationSource,
                        _navigationSelectItem.SelectAndExpand,
                        _navigationSelectItem.FilterOption,
                        orderByClause,
                        _navigationSelectItem.TopOption,
                        _navigationSelectItem.SkipOption,
                        _navigationSelectItem.CountOption,
                        _navigationSelectItem.SearchOption,
                        _navigationSelectItem.LevelsOption);
            }

            foreach (OeNavigationSelectItem childNavigationItem in _navigationItems)
            {
                if (!AllSelected && childNavigationItem.Kind == OeNavigationSelectItemKind.NextLink && !childNavigationItem.EdmProperty!.Type.IsCollection())
                    foreach (IEdmStructuralProperty fkeyProperty in childNavigationItem.EdmProperty.DependentProperties())
                        AddStructuralItem(fkeyProperty, notSelected);

                childNavigationItem.AddKeyRecursive(notSelected);
            }
        }
        internal OeNavigationSelectItem AddOrGetNavigationItem(OeNavigationSelectItem navigationItem, bool isExpand)
        {
            _allSelected &= isExpand;
            OeNavigationSelectItem? existingNavigationItem = FindChildrenNavigationItem(navigationItem.EdmProperty);
            if (existingNavigationItem == null)
            {
                _navigationItems.Add(navigationItem);
                return navigationItem;
            }

            if (existingNavigationItem.Kind == OeNavigationSelectItemKind.NotSelected && navigationItem.Kind == OeNavigationSelectItemKind.Normal)
                existingNavigationItem.Kind = OeNavigationSelectItemKind.Normal;

            return existingNavigationItem;
        }
        public bool AddStructuralItem(IEdmStructuralProperty structuralProperty, bool notSelected)
        {
            if (notSelected)
            {
                if (FindStructuralItemIndex(_structuralItemsNotSelected, structuralProperty) != -1)
                    return false;

                if (FindStructuralItemIndex(_structuralItems, structuralProperty) != -1)
                    return false;

                _structuralItemsNotSelected.Add(new OeStructuralSelectItem(structuralProperty, notSelected));
            }
            else
            {
                if (FindStructuralItemIndex(_structuralItems, structuralProperty) != -1)
                    return false;

                int i = FindStructuralItemIndex(_structuralItemsNotSelected, structuralProperty);
                if (i != -1)
                    _structuralItemsNotSelected.RemoveAt(i);

                _structuralItems.Add(new OeStructuralSelectItem(structuralProperty, notSelected));
            }
            return true;
        }
        private static ODataPath CreatePath(ODataPath parentPath, ODataExpandPath pathToNavigationProperty)
        {
            var segments = new List<ODataPathSegment>(parentPath);
            segments.AddRange(pathToNavigationProperty);
            return new ODataPath(segments);
        }
        private OeNavigationSelectItem? FindChildrenNavigationItem(IEdmNavigationProperty navigationProperty)
        {
            for (int i = 0; i < _navigationItems.Count; i++)
                if (_navigationItems[i].EdmProperty == navigationProperty)
                    return _navigationItems[i];
            return null;
        }
        public OeNavigationSelectItem? FindHierarchyNavigationItem(IEdmNavigationProperty navigationProperty)
        {
            if (Parent != null)
            {
                if (EdmProperty == navigationProperty)
                    return this;

                for (int i = 0; i < _navigationItems.Count; i++)
                {
                    OeNavigationSelectItem? matched = _navigationItems[i].FindHierarchyNavigationItem(navigationProperty);
                    if (matched != null)
                        return matched;
                }
            }

            return null;
        }
        private static int FindStructuralItemIndex(List<OeStructuralSelectItem> structuralItems, IEdmProperty structuralProperty)
        {
            for (int i = 0; i < structuralItems.Count; i++)
                if (structuralItems[i].EdmProperty == structuralProperty)
                    return i;
            return -1;
        }
        private static IEdmEntitySet GetEntitySet(ODataPath path)
        {
            if (path.LastSegment is EntitySetSegment entitySetSegment)
                return entitySetSegment.EntitySet;

            if (path.LastSegment is NavigationPropertySegment navigationPropertySegment)
                return (IEdmEntitySet)navigationPropertySegment.NavigationSource;

            if (path.LastSegment is KeySegment keySegment)
                return (IEdmEntitySet)keySegment.NavigationSource;

            if (path.LastSegment is OperationSegment)
                return ((EntitySetSegment)path.FirstSegment).EntitySet;

            if (path.LastSegment is FilterSegment)
                return ((EntitySetSegment)path.FirstSegment).EntitySet;

            throw new InvalidOperationException("unknown segment type " + path.LastSegment.ToString());
        }
        public IReadOnlyList<IEdmNavigationProperty> GetJoinPath()
        {
            var joinPath = new List<IEdmNavigationProperty>();
            for (OeNavigationSelectItem navigationItem = this; navigationItem.Parent != null; navigationItem = navigationItem.Parent)
                joinPath.Insert(0, navigationItem.EdmProperty!);
            return joinPath;
        }
        public IReadOnlyList<OeStructuralSelectItem> GetStructuralItemsWithNotSelected()
        {
            if (AllSelected)
                throw new InvalidOperationException("StructuralItems.Count > 0");

            if (_structuralItemsNotSelected.Count == 0)
                return _structuralItems;

            var allStructuralItems = new List<OeStructuralSelectItem>(_structuralItems.Count + _structuralItemsNotSelected.Count);
            allStructuralItems.AddRange(_structuralItems);

            for (int i = 0; i < _structuralItemsNotSelected.Count; i++)
                if (FindStructuralItemIndex(allStructuralItems, _structuralItemsNotSelected[i].EdmProperty) == -1)
                    allStructuralItems.Add(_structuralItemsNotSelected[i]);

            return allStructuralItems;
        }
        public bool HasNavigationItems()
        {
            for (int i = 0; i < _navigationItems.Count; i++)
                if (_navigationItems[i].Kind != OeNavigationSelectItemKind.NextLink)
                    return true;

            return false;
        }

        public bool AlreadyUsedInBuildExpression { get; set; }
        public IEdmNavigationProperty EdmProperty => _edmProperty ?? throw new InvalidOperationException(nameof(EdmProperty) + " missing for when Parent is null");
        public IEdmEntitySetBase EntitySet { get; }
        public OeEntryFactory EntryFactory
        {
            get => _entryFactory!;
            set => _entryFactory = value ?? throw new ArgumentNullException(nameof(value));
        }
        public OeNavigationSelectItemKind Kind { get; private set; }
        public IReadOnlyList<OeNavigationSelectItem> NavigationItems => _navigationItems;
        public ExpandedNavigationSelectItem NavigationSelectItem => _navigationSelectItem ?? throw new InvalidOperationException(nameof(NavigationSelectItem) + " missing for when Parent is null");
        public int PageSize { get; set; }
        public OeNavigationSelectItem? Parent { get; }
        public ODataPath Path { get; }
        public bool AllSelected => _allSelected && _structuralItems.Count == 0;
    }

    internal readonly struct OeStructuralSelectItem
    {
        public OeStructuralSelectItem(IEdmStructuralProperty edmProperty, bool notSelected)
        {
            EdmProperty = edmProperty;
            NotSelected = notSelected;
        }

        public IEdmStructuralProperty EdmProperty { get; }
        public bool NotSelected { get; }
    }
}
