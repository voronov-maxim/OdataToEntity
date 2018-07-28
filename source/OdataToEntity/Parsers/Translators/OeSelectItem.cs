using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace OdataToEntity.Parsers.Translators
{
    internal sealed class OeSelectItem
    {
        private readonly List<OeSelectItem> _navigationItems;
        private readonly List<OeSelectItem> _selectItems;

        public OeSelectItem(IEdmProperty edmProperty, bool skipToken) : this(null, null, null, edmProperty, null, null, skipToken)
        {
        }
        public OeSelectItem(OeSelectItem parent, IEdmEntitySet entitySet, ODataPath path, IEdmProperty edmProperty, ODataNestedResourceInfo resource, bool? countOption, bool skipToken)
        {
            Parent = parent;
            EntitySet = entitySet;
            Path = path;
            EdmProperty = edmProperty;
            Resource = resource;
            CountOption = countOption;
            SkipToken = skipToken;

            _navigationItems = new List<OeSelectItem>();
            _selectItems = new List<OeSelectItem>();
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
        public IReadOnlyList<IEdmNavigationProperty> GetJoinPath()
        {
            var joinPath = new List<IEdmNavigationProperty>();
            for (OeSelectItem navigationItem = this; navigationItem.Parent != null; navigationItem = navigationItem.Parent)
                joinPath.Insert(0, (IEdmNavigationProperty)navigationItem.EdmProperty);
            return joinPath;
        }

        public bool? CountOption { get; }
        public IEdmProperty EdmProperty { get; }
        public IEdmEntitySet EntitySet { get; }
        public OeEntryFactory EntryFactory { get; set; }
        public bool HasNavigationItems => _navigationItems.Count > 0;
        public bool HasSelectItems => _selectItems.Count > 0;
        public IReadOnlyList<OeSelectItem> NavigationItems => _navigationItems;
        public OeSelectItem Parent { get; }
        public ODataPath Path { get; }
        public ODataNestedResourceInfo Resource { get; }
        public IReadOnlyList<OeSelectItem> SelectItems => _selectItems;
        public bool SkipToken { get; }
    }
}
