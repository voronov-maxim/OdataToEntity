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

        public void AddEntity(Object dataContext, Object entity)
        {
            _entitySetMetaAdapter.AddEntity(dataContext, entity);
        }
        public void AttachEntity(Object dataContext, Object entity)
        {
            _entitySetMetaAdapter.AttachEntity(dataContext, entity);
        }
        public IQueryable GetEntitySet(Object dataContext)
        {
            return _entitySetMetaAdapter.GetEntitySet(dataContext);
        }
        public void RemoveEntity(Object dataContext, Object entity)
        {
            _entitySetMetaAdapter.RemoveEntity(dataContext, entity);
        }

        public OeDataAdapter DataAdapter
        {
            get
            {
                return _dataAdapter;
            }
        }
        public OeEntitySetMetaAdapter EntitySetMetaAdapter
        {
            get
            {
                return _entitySetMetaAdapter;
            }
        }
        public Type EntityType
        {
            get
            {
                return _entitySetMetaAdapter.EntityType;
            }
        }
    }
}
