using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System;
using System.Collections.Generic;

namespace OdataToEntity.Parsers.Translators
{
    internal sealed class OeNavigationSelectItem
    {
        private readonly List<OeNavigationSelectItem> _navigationItems;
        private bool _selectAll;
        private readonly List<OeStructuralSelectItem> _structuralItems;
        private readonly List<OeStructuralSelectItem> _structuralItemsSkipToken;

        private OeNavigationSelectItem()
        {
            _navigationItems = new List<OeNavigationSelectItem>();
            _structuralItems = new List<OeStructuralSelectItem>();
            _structuralItemsSkipToken = new List<OeStructuralSelectItem>();
        }
        public OeNavigationSelectItem(ODataUri odataUri) : this()
        {
            EntitySet = GetEntitySet(odataUri.Path);
            Path = odataUri.Path;
        }
        public OeNavigationSelectItem(IEdmEntitySetBase entitySet, OeNavigationSelectItem parent, ExpandedNavigationSelectItem item, bool skipToken) : this()
        {
            var segment = (NavigationPropertySegment)item.PathToNavigationProperty.LastSegment;

            var segments = new List<ODataPathSegment>(parent.Path);
            segments.AddRange(item.PathToNavigationProperty);

            EdmProperty = segment.NavigationProperty;
            EntitySet = entitySet;
            NavigationSelectItem = item;
            Parent = parent;
            Path = new ODataPath(segments);
            SkipToken = skipToken;
        }

        internal void AddKeyRecursive(bool skipToken)
        {
            if (StructuralItems.Count > 0)
                foreach (IEdmStructuralProperty keyProperty in EntitySet.EntityType().Key())
                    AddStructuralItem(keyProperty, skipToken);

            foreach (OeNavigationSelectItem childNavigationItem in _navigationItems)
                childNavigationItem.AddKeyRecursive(skipToken);
        }
        internal OeNavigationSelectItem AddOrGetNavigationItem(OeNavigationSelectItem navigationItem)
        {
            OeNavigationSelectItem existingNavigationItem = FindChildrenNavigationItem(navigationItem.EdmProperty);
            if (existingNavigationItem == null)
            {
                _navigationItems.Add(navigationItem);
                return navigationItem;
            }

            if (existingNavigationItem.SkipToken && !navigationItem.SkipToken)
                existingNavigationItem.SkipToken = false;
            else if (existingNavigationItem.StructuralItems.Count == 0)
                existingNavigationItem._selectAll = true;

            return existingNavigationItem;
        }
        public bool AddStructuralItem(IEdmStructuralProperty structuralProperty, bool skipToken)
        {
            if (_selectAll)
                return false;

            if (skipToken)
            {
                if (FindStructuralItemIndex(_structuralItemsSkipToken, structuralProperty) != -1)
                    return false;

                if (FindStructuralItemIndex(_structuralItems, structuralProperty) != -1)
                    return false;

                _structuralItemsSkipToken.Add(new OeStructuralSelectItem(structuralProperty, skipToken));
            }
            else
            {
                if (FindStructuralItemIndex(_structuralItems, structuralProperty) != -1)
                    return false;

                int i = FindStructuralItemIndex(_structuralItemsSkipToken, structuralProperty);
                if (i != -1)
                    _structuralItemsSkipToken.RemoveAt(i);

                _structuralItems.Add(new OeStructuralSelectItem(structuralProperty, skipToken));
            }
            return true;
        }
        private OeNavigationSelectItem FindChildrenNavigationItem(IEdmNavigationProperty navigationProperty)
        {
            for (int i = 0; i < _navigationItems.Count; i++)
                if (_navigationItems[i].EdmProperty == navigationProperty)
                    return _navigationItems[i];
            return null;
        }
        public OeNavigationSelectItem FindHierarchyNavigationItem(IEdmNavigationProperty navigationProperty)
        {
            if (EdmProperty == navigationProperty)
                return this;

            for (int i = 0; i < _navigationItems.Count; i++)
            {
                OeNavigationSelectItem matched = _navigationItems[i].FindHierarchyNavigationItem(navigationProperty);
                if (matched != null)
                    return matched;
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
                joinPath.Insert(0, navigationItem.EdmProperty);
            return joinPath;
        }
        public IReadOnlyList<OeStructuralSelectItem> GetStructuralItemsWithSkipToken()
        {
            if (_structuralItems.Count == 0)
                throw new InvalidOperationException("StructuralItems.Count > 0");

            if (_structuralItemsSkipToken.Count == 0)
                return _structuralItems;

            var allStructuralItems = new List<OeStructuralSelectItem>(_structuralItems.Count + _structuralItemsSkipToken.Count);
            allStructuralItems.AddRange(_structuralItems);

            for (int i = 0; i < _structuralItemsSkipToken.Count; i++)
                if (FindStructuralItemIndex(allStructuralItems, _structuralItemsSkipToken[i].EdmProperty) == -1)
                    allStructuralItems.Add(_structuralItemsSkipToken[i]);

            return allStructuralItems;
        }

        internal bool AlreadyUsedInBuildExpression { get; set; }
        public IEdmNavigationProperty EdmProperty { get; }
        public IEdmEntitySetBase EntitySet { get; }
        public OeEntryFactory EntryFactory { get; set; }
        public IReadOnlyList<OeNavigationSelectItem> NavigationItems => _navigationItems;
        public ExpandedNavigationSelectItem NavigationSelectItem { get; }
        public int PageSize { get; set; }
        public OeNavigationSelectItem Parent { get; }
        public ODataPath Path { get; }
        public bool SkipToken { get; private set; }
        public IReadOnlyList<OeStructuralSelectItem> StructuralItems => _structuralItems;
    }

    internal readonly struct OeStructuralSelectItem
    {
        public OeStructuralSelectItem(IEdmStructuralProperty edmProperty, bool skipToken)
        {
            EdmProperty = edmProperty;
            SkipToken = skipToken;
        }

        public IEdmStructuralProperty EdmProperty { get; }
        public bool SkipToken { get; }
    }
}
