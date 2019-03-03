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
        private readonly List<OeStructuralSelectItem> _structualItems;

        private OeNavigationSelectItem()
        {
            _navigationItems = new List<OeNavigationSelectItem>();
            _structualItems = new List<OeStructuralSelectItem>();
        }
        public OeNavigationSelectItem(IEdmModel edmModel, ODataUri odataUri) : this()
        {
            EntitySet = GetEntitySet(odataUri.Path);
            Path = odataUri.Path;

            ApplyModelBoundAttribute(edmModel);
            CountOption = CountOption ?? odataUri.QueryCount;
        }
        public OeNavigationSelectItem(IEdmModel edmModel, OeNavigationSelectItem parent, ExpandedNavigationSelectItem item, bool skipToken) : this()
        {
            var segment = (NavigationPropertySegment)item.PathToNavigationProperty.LastSegment;

            var segments = new List<ODataPathSegment>(parent.Path);
            segments.AddRange(item.PathToNavigationProperty);

            EdmProperty = segment.NavigationProperty;
            EntitySet = (IEdmEntitySetBase)item.NavigationSource ?? OeEdmClrHelper.GetEntitySet(edmModel, segment.NavigationProperty);
            NavigationSelectItem = item;
            Parent = parent;
            Path = new ODataPath(segments);
            SkipToken = skipToken;

            if (segment.NavigationProperty.Type.IsCollection() && !segment.NavigationProperty.ContainsTarget)
            {
                ApplyModelBoundAttribute(edmModel);
                CountOption = CountOption ?? item.CountOption;
            }
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

            return existingNavigationItem;
        }
        public bool AddStructuralItem(IEdmStructuralProperty structuralProperty, bool skipToken)
        {
            var structuralItem = new OeStructuralSelectItem(structuralProperty, skipToken);
            int existsSelectItemIndex;
            if ((existsSelectItemIndex = FindSelectItem(structuralItem.EdmProperty)) != -1)
            {
                if (_structualItems[existsSelectItemIndex].SkipToken && !structuralItem.SkipToken)
                    _structualItems[existsSelectItemIndex] = structuralItem;
                return false;
            }

            _structualItems.Add(structuralItem);
            return true;
        }
        private void ApplyModelBoundAttribute(IEdmModel edmModel)
        {
            Query.PageAttribute pageAttribute;
            if (EdmProperty == null)
                pageAttribute = edmModel.GetModelBoundAttribute<Query.PageAttribute>(EntitySet.EntityType());
            else
                pageAttribute = edmModel.GetModelBoundAttribute<Query.PageAttribute>(EdmProperty);
            if (pageAttribute != null)
            {
                MaxTop = pageAttribute.MaxTop;
                PageSize = pageAttribute.PageSize;
            }

            Query.CountAttribute countAttribute;
            if (EdmProperty == null)
                countAttribute = edmModel.GetModelBoundAttribute<Query.CountAttribute>(EntitySet.EntityType());
            else
                countAttribute = edmModel.GetModelBoundAttribute<Query.CountAttribute>(EdmProperty);
            if (countAttribute != null)
                CountOption = !countAttribute.Disabled;
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
        private int FindSelectItem(IEdmProperty structuralProperty)
        {
            for (int i = 0; i < _structualItems.Count; i++)
                if (_structualItems[i].EdmProperty == structuralProperty)
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

        public bool AlreadyUsedInBuildExpression { get; set; }
        public bool? CountOption { get; private set; }
        public IEdmNavigationProperty EdmProperty { get; }
        public IEdmEntitySetBase EntitySet { get; }
        public OeEntryFactory EntryFactory { get; set; }
        public int MaxTop { get; private set; }
        public IReadOnlyList<OeNavigationSelectItem> NavigationItems => _navigationItems;
        public ExpandedNavigationSelectItem NavigationSelectItem { get; }
        public int PageSize { get; private set; }
        public OeNavigationSelectItem Parent { get; }
        public ODataPath Path { get; }
        public bool SkipToken { get; }
        public IReadOnlyList<OeStructuralSelectItem> StructuralItems => _structualItems;
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
