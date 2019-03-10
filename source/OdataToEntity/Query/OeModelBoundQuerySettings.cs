using Microsoft.OData.Edm;
using System;
using System.Collections.Generic;

namespace OdataToEntity.Query
{
    public sealed class OeModelBoundQuerySettings
    {
        public OeModelBoundQuerySettings()
        {
            Countable = true;
            Expandable = true;
            MaxTop = Int32.MinValue;
            NotOrderableCollection = new HashSet<IEdmProperty>();
            Orderable = true;
        }

        public bool Countable { get; set; }
        public bool Expandable { get; set; }
        public int MaxTop { get; set; }
        public HashSet<IEdmProperty> NotOrderableCollection { get; }
        public bool Orderable { get; set; }
    }
}
