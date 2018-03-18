using Microsoft.OData;
using Microsoft.OData.Edm;

namespace OdataToEntity
{
    public sealed class OeEntityItem
    {
        public OeEntityItem(IEdmEntitySet entitySet, IEdmEntityType entityType, ODataResource entry)
        {
            EntitySet = entitySet;
            EntityType = entityType;
            Entry = entry;
        }

        public ODataResource Entry { get; }
        public IEdmEntitySet EntitySet { get; }
        public IEdmEntityType EntityType { get; }
    }
}
