using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace OdataToEntity.Parsers.Translators
{
    internal sealed class OeSelectEntryFactoryFactory : OeEntryFactoryFactory
    {
        private readonly OeNavigationSelectItem _rootNavigationItem;

        public OeSelectEntryFactoryFactory(OeNavigationSelectItem rootNavigationItem)
        {
            _rootNavigationItem = rootNavigationItem;
        }

        public override OeEntryFactory CreateEntryFactory(IEdmEntitySet entitySet, Type clrType, OePropertyAccessor[]? skipTokenAccessors)
        {
            ParameterExpression parameter = Expression.Parameter(typeof(Object));
            UnaryExpression typedParameter = Expression.Convert(parameter, clrType);

            if (_rootNavigationItem.HasNavigationItems())
            {
                List<OeNavigationSelectItem> navigationItems = OeSelectTranslator.FlattenNavigationItems(_rootNavigationItem, true);
                IReadOnlyList<MemberExpression> navigationProperties = OeExpressionHelper.GetPropertyExpressions(typedParameter);
                int propertyIndex = navigationProperties.Count - 1;

                for (int i = navigationItems.Count - 1; i >= 0; i--)
                {
                    OeNavigationSelectItem navigationItem = navigationItems[i];
                    if (navigationItem.Kind == OeNavigationSelectItemKind.NotSelected)
                    {
                        propertyIndex--;
                        continue;
                    }

                    OePropertyAccessor[] accessors = Array.Empty<OePropertyAccessor>();
                    LambdaExpression? linkAccessor = null;
                    OeNavigationEntryFactory[] nestedNavigationLinks = Array.Empty<OeNavigationEntryFactory>();
                    if (navigationItem.Kind != OeNavigationSelectItemKind.NextLink)
                    {
                        accessors = GetAccessors(navigationProperties[propertyIndex].Type, navigationItem);
                        linkAccessor = Expression.Lambda(navigationProperties[propertyIndex], parameter);
                        nestedNavigationLinks = GetNestedNavigationLinks(navigationItem);
                        propertyIndex--;
                    }

                    if (i == 0)
                        navigationItem.EntryFactory = new OeEntryFactory(navigationItem.EntitySet, accessors, skipTokenAccessors, nestedNavigationLinks, linkAccessor);
                    else
                        navigationItem.EntryFactory = new OeNavigationEntryFactory(
                            navigationItem.EntitySet,
                            accessors,
                            skipTokenAccessors,
                            nestedNavigationLinks,
                            linkAccessor,
                            navigationItem.EdmProperty,
                            navigationItem.NavigationSelectItem,
                            navigationItem.Kind == OeNavigationSelectItemKind.NextLink);
                }
            }
            else
            {
                var navigationLinks = new OeNavigationEntryFactory[_rootNavigationItem.NavigationItems.Count];
                for (int i = 0; i < _rootNavigationItem.NavigationItems.Count; i++)
                {
                    OeNavigationSelectItem navigationItem = _rootNavigationItem.NavigationItems[i];
                    navigationLinks[i] = new OeNavigationEntryFactory(
                        navigationItem.EntitySet,
                        Array.Empty<OePropertyAccessor>(),
                        null,
                        Array.Empty<OeNavigationEntryFactory>(),
                        null,
                        navigationItem.EdmProperty,
                        navigationItem.NavigationSelectItem,
                        navigationItem.Kind == OeNavigationSelectItemKind.NextLink);
                }

                OePropertyAccessor[] accessors = GetAccessors(clrType, _rootNavigationItem);
                _rootNavigationItem.EntryFactory = new OeEntryFactory(_rootNavigationItem.EntitySet, accessors, skipTokenAccessors, navigationLinks);
            }

            return _rootNavigationItem.EntryFactory;
        }
        private static OePropertyAccessor[] GetAccessors(Type clrEntityType, OeNavigationSelectItem navigationItem)
        {
            ParameterExpression parameter;
            UnaryExpression typedAccessorParameter;
            IReadOnlyList<MemberExpression> propertyExpressions;

            if (navigationItem.AllSelected)
            {
                if (navigationItem.Parent != null && navigationItem.NavigationSelectItem is ExpandedCountSelectItem)
                {
                    parameter = Expression.Parameter(typeof(Object));
                    typedAccessorParameter = Expression.Convert(parameter, clrEntityType);
                    propertyExpressions = OeExpressionHelper.GetPropertyExpressions(typedAccessorParameter);
                    return new[] { OePropertyAccessor.CreatePropertyAccessor(OeEdmClrHelper.CountProperty, propertyExpressions[0], parameter, false) };
                }
                else
                    return OePropertyAccessor.CreateFromType(clrEntityType, navigationItem.EntitySet);
            }

            parameter = Expression.Parameter(typeof(Object));
            typedAccessorParameter = Expression.Convert(parameter, clrEntityType);
            propertyExpressions = OeExpressionHelper.GetPropertyExpressions(typedAccessorParameter);

            IReadOnlyList<OeStructuralSelectItem> structuralItems = navigationItem.GetStructuralItemsWithNotSelected();
            var accessors = new OePropertyAccessor[structuralItems.Count];
            for (int i = 0; i < structuralItems.Count; i++)
                accessors[i] = OePropertyAccessor.CreatePropertyAccessor(structuralItems[i].EdmProperty, propertyExpressions[i], parameter, structuralItems[i].NotSelected);

            return accessors;
        }
        private static OeNavigationEntryFactory[] GetNestedNavigationLinks(OeNavigationSelectItem navigationItem)
        {
            var nestedEntryFactories = new List<OeNavigationEntryFactory>(navigationItem.NavigationItems.Count);
            for (int i = 0; i < navigationItem.NavigationItems.Count; i++)
                if (navigationItem.NavigationItems[i].Kind != OeNavigationSelectItemKind.NotSelected)
                    nestedEntryFactories.Add((OeNavigationEntryFactory)navigationItem.NavigationItems[i].EntryFactory);
            return nestedEntryFactories.ToArray();
        }
    }
}
