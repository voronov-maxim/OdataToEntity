using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System;
using System.Collections.Generic;

namespace OdataToEntity.Query
{
    public sealed class OeModelBoundQuerySettings
    {
        public OeModelBoundQuerySettings()
        {
            Countable = true;
            Filterable = true;
            MaxTop = Int32.MinValue;
            NotFilterableCollection = new HashSet<IEdmProperty>();
            NotOrderableCollection = new HashSet<IEdmProperty>();
            Orderable = true;
            Selectable = true;
            SelectExpandItems = Array.Empty<SelectItem>();
        }

        public bool Countable { get; set; }
        public bool Filterable { get; set; }
        public int MaxTop { get; set; }
        public HashSet<IEdmProperty> NotFilterableCollection { get; }
        public HashSet<IEdmProperty> NotOrderableCollection { get; }
        public bool Orderable { get; set; }
        public bool Selectable { get; set; }
        public SelectItem[] SelectExpandItems { get; set; }
    }
}
