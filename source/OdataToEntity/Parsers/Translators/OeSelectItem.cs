using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System;
using System.Collections.Generic;

namespace OdataToEntity.Parsers.Translators
{
    internal sealed class OeSelectItem
    {
        private readonly List<OeSelectItem> _navigationItems;
        private readonly List<OeSelectItem> _selectItems;

        private OeSelectItem()
        {
            _navigationItems = new List<OeSelectItem>();
            _selectItems = new List<OeSelectItem>();
        }
        public OeSelectItem(ODataPath path) : this()
        {
            EntitySet = GetEntitySet(path);
            Path = path;
        }
        public OeSelectItem(IEdmProperty edmProperty, bool skipToken) : this()
        {
            EdmProperty = edmProperty;
            SkipToken = skipToken;
        }
        public OeSelectItem(IEdmModel edmModel, OeSelectItem parent, ExpandedNavigationSelectItem expandedNavigationSelectItem, bool skipToken) : this()
        {
            var segment = (NavigationPropertySegment)expandedNavigationSelectItem.PathToNavigationProperty.LastSegment;

            var segments = new List<ODataPathSegment>(parent.Path);
            segments.AddRange(expandedNavigationSelectItem.PathToNavigationProperty);

            EdmProperty = segment.NavigationProperty;
            EntitySet = (IEdmEntitySetBase)expandedNavigationSelectItem.NavigationSource ?? OeEdmClrHelper.GetEntitySet(edmModel, segment.NavigationProperty);
            ExpandedNavigationSelectItem = expandedNavigationSelectItem;
            Parent = parent;
            Path = new ODataPath(segments);
            SkipToken = skipToken;
        }

        public void AddNavigationItem(OeSelectItem navigationItem)
        {
            _navigationItems.Add(navigationItem);
        }
        public bool AddSelectItem(OeSelectItem selectItem)
        {
            int existsSelectItemIndex;
            if ((existsSelectItemIndex = FindSelectItem(selectItem.EdmProperty)) != -1)
            {
                if (_selectItems[existsSelectItemIndex].SkipToken && !selectItem.SkipToken)
                    _selectItems[existsSelectItemIndex] = selectItem;
                return false;
            }

            _selectItems.Add(selectItem);
            return true;
        }
        public OeSelectItem FindChildrenNavigationItem(IEdmNavigationProperty navigationProperty)
        {
            for (int i = 0; i < _navigationItems.Count; i++)
                if (_navigationItems[i].EdmProperty == navigationProperty)
                    return _navigationItems[i];
            return null;
        }
        public OeSelectItem FindHierarchyNavigationItem(IEdmNavigationProperty navigationProperty)
        {
            if (EdmProperty == navigationProperty)
                return this;

            for (int i = 0; i < _navigationItems.Count; i++)
            {
                OeSelectItem matched = _navigationItems[i].FindHierarchyNavigationItem(navigationProperty);
                if (matched != null)
                    return matched;
            }

            return null;
        }
        private int FindSelectItem(IEdmProperty structuralProperty)
        {
            for (int i = 0; i < _selectItems.Count; i++)
                if (_selectItems[i].EdmProperty == structuralProperty)
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

            throw new InvalidOperationException("unknown segment type " + path.LastSegment.ToString());
        }
        public IReadOnlyList<IEdmNavigationProperty> GetJoinPath()
        {
            var joinPath = new List<IEdmNavigationProperty>();
            for (OeSelectItem navigationItem = this; navigationItem.Parent != null; navigationItem = navigationItem.Parent)
                joinPath.Insert(0, (IEdmNavigationProperty)navigationItem.EdmProperty);
            return joinPath;
        }

        public IEdmProperty EdmProperty { get; }
        public IEdmEntitySetBase EntitySet { get; }
        public OeEntryFactory EntryFactory { get; set; }
        public ExpandedNavigationSelectItem ExpandedNavigationSelectItem { get; }
        public bool HasNavigationItems => _navigationItems.Count > 0;
        public IReadOnlyList<OeSelectItem> NavigationItems => _navigationItems;
        public OeSelectItem Parent { get; }
        public ODataPath Path { get; }
        public IReadOnlyList<OeSelectItem> SelectItems => _selectItems;
        public bool SkipToken { get; }
    }
}
