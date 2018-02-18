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

        public void AddEntity(Object dataContext, Object entity) => _entitySetMetaAdapter.AddEntity(dataContext, entity);
        public void AttachEntity(Object dataContext, Object entity) => _entitySetMetaAdapter.AttachEntity(dataContext, entity);
        public IQueryable GetEntitySet(Object dataContext) => _entitySetMetaAdapter.GetEntitySet(dataContext);
        public void RemoveEntity(Object dataContext, Object entity) => _entitySetMetaAdapter.RemoveEntity(dataContext, entity);

        public OeDataAdapter DataAdapter => _dataAdapter;
        public OeEntitySetMetaAdapter EntitySetMetaAdapter => _entitySetMetaAdapter;
        public Type EntityType => _entitySetMetaAdapter.EntityType;
    }
}
