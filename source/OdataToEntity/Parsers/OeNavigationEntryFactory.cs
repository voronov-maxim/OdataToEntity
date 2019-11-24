using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace OdataToEntity.Parsers
{
    public class OeNavigationEntryFactory : OeEntryFactory
    {
        public OeNavigationEntryFactory(
            IEdmEntitySetBase entitySet,
            OePropertyAccessor[] accessors,
            OePropertyAccessor[]? skipTokenAccessors,
            IReadOnlyList<OeNavigationEntryFactory> navigationLinks,
            LambdaExpression? linkAccessor,
            IEdmNavigationProperty edmNavigationProperty,
            ExpandedNavigationSelectItem navigationSelectItem,
            bool nextLink)
            : base(entitySet, accessors, skipTokenAccessors, navigationLinks, linkAccessor)
        {
            EdmNavigationProperty = edmNavigationProperty;
            NavigationSelectItem = navigationSelectItem;
            NextLink = nextLink;
        }

        protected override OeNavigationEntryFactory CreateEntryFactoryFromTuple(IEdmModel edmModel, OeEntryFactory parentEntryFactory)
        {
            OePropertyAccessor[] accessors = base.GetAccessorsFromTuple(edmModel);
            var navigationLinks = new OeNavigationEntryFactory[NavigationLinks.Count];
            for (int i = 0; i < NavigationLinks.Count; i++)
                navigationLinks[i] = NavigationLinks[i].CreateEntryFactoryFromTuple(edmModel, this);

            ParameterExpression parameter = Expression.Parameter(typeof(Object));
            UnaryExpression typedParameter = Expression.Convert(parameter, edmModel.GetClrType(parentEntryFactory.EntitySet));
            MemberExpression navigationPropertyExpression = Expression.Property(typedParameter, EdmNavigationProperty.Name);
            LambdaExpression linkAccessor = Expression.Lambda(navigationPropertyExpression, parameter);

            return new OeNavigationEntryFactory(
                EntitySet,
                accessors,
                null,
                navigationLinks,
                linkAccessor,
                EdmNavigationProperty,
                NavigationSelectItem,
                false);
        }

        public IEdmNavigationProperty EdmNavigationProperty { get; }
        public ExpandedNavigationSelectItem NavigationSelectItem { get; }
        public bool NextLink { get; }
    }
}
