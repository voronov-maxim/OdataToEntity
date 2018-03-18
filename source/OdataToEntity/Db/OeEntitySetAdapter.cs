using Microsoft.OData;
using System;
using System.Linq;

namespace OdataToEntity.Db
{
    public struct OeEntitySetAdapter
    {
        private readonly OeDataAdapter _dataAdapter;
        private readonly OeEntitySetMetaAdapter _entitySetMetaAdapter;

        public OeEntitySetAdapter(OeEntitySetMetaAdapter entitySetMetaAdapter, OeDataAdapter dataAdapter)
        {
            _entitySetMetaAdapter = entitySetMetaAdapter;
            _dataAdapter = dataAdapter;
        }

        public void AddEntity(Object dataContext, ODataResourceBase entry) => _entitySetMetaAdapter.AddEntity(dataContext, entry);
        public void AttachEntity(Object dataContext, ODataResourceBase entry) => _entitySetMetaAdapter.AttachEntity(dataContext, entry);
        public IQueryable GetEntitySet(Object dataContext) => _entitySetMetaAdapter.GetEntitySet(dataContext);
        public void RemoveEntity(Object dataContext, ODataResourceBase entry) => _entitySetMetaAdapter.RemoveEntity(dataContext, entry);

        public OeDataAdapter DataAdapter => _dataAdapter;
        public OeEntitySetMetaAdapter EntitySetMetaAdapter => _entitySetMetaAdapter;
        public Type EntityType => _entitySetMetaAdapter.EntityType;
    }
}
