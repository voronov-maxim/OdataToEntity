using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System;
using System.Collections.Generic;

namespace OdataToEntity.Query.Builder
{
    public sealed class OeModelBoundEntitySettings
    {
        private OeModelBoundEntitySettings()
        {
            Countable = true;
            MaxTop = Int32.MinValue;
            PageSize = 0;
            SelectExpandItems = Array.Empty<SelectItem>();

            Properties = new Dictionary<IEdmProperty, OeModelBoundPropertySettings>();
        }
        public OeModelBoundEntitySettings(IEdmEntityType entityType) : this()
        {
            EntityType = entityType;
        }
        public OeModelBoundEntitySettings(IEdmNavigationProperty navigationProperty) : this()
        {
            NavigationProperty = navigationProperty;
        }

        public bool IsAllowed(OeModelBoundProvider modelBoundProvider, IEdmProperty property, OeModelBoundKind modelBoundKind)
        {
            bool? isAllowed = IsAllowed(modelBoundKind, property);
            if (isAllowed != null)
                return isAllowed.Value;

            if (NavigationProperty != null)
            {
                OeModelBoundEntitySettings entitySettings = modelBoundProvider.TryGetQuerySettings((IEdmEntityType)property.DeclaringType);
                if (entitySettings != null && entitySettings.IsAllowed(modelBoundKind, property) == false)
                    return false;
            }

            return true;
        }
        public bool? IsAllowed(OeModelBoundKind modelBoundKind, IEdmProperty property)
        {
            if (Properties.TryGetValue(property, out OeModelBoundPropertySettings propertySettings))
                return propertySettings.IsAllowed(modelBoundKind);

            return null;
        }
        public OeModelBoundPropertySettings GetPropertySettings(IEdmProperty property)
        {
            Properties.TryGetValue(property, out OeModelBoundPropertySettings propertySettings);
            return propertySettings;
        }

        public bool Countable { get; set; }
        public int MaxTop { get; set; }
        public int PageSize { get; set; }
        public SelectItem[] SelectExpandItems { get; set; }

        public Dictionary<IEdmProperty, OeModelBoundPropertySettings> Properties { get; }

        public IEdmEntityType EntityType { get; }
        public IEdmNavigationProperty NavigationProperty { get; }
    }

    public sealed class OeModelBoundPropertySettings
    {
        public bool? IsAllowed(OeModelBoundKind modelBoundKind)
        {
            switch (modelBoundKind)
            {
                case OeModelBoundKind.Expand:
                case OeModelBoundKind.Select:
                    return SelectType == null ? (bool?)null : SelectType.Value != SelectExpandType.Disabled;
                case OeModelBoundKind.Filter:
                    return Filterable;
                case OeModelBoundKind.OrderBy:
                    return Orderable;
                default:
                    throw new InvalidOperationException("Unknown OeModelBoundKind " + modelBoundKind.ToString());
            }
        }

        public bool? Filterable { get; set; }
        public bool? Orderable { get; set; }
        public int? PageSize { get; set; }
        public SelectExpandType? SelectType { get; set; }
    }
}
