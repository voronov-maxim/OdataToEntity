using Microsoft.OData;
using OdataToEntity.Db;
using OdataToEntity.Parsers;
using System;

namespace OdataToEntity.AspNetCore
{
    public sealed class OeDataContext
    {
        private readonly Object _dataContext;
        private readonly OeEntitySetAdapter _entitySetAdapter;
        private readonly OeOperationMessage _operation;

        public OeDataContext()
        {
        }

        public OeDataContext(ref OeEntitySetAdapter entitySetAdapter, Object dataContext, OeOperationMessage operation)
        {
            _entitySetAdapter = entitySetAdapter;
            _dataContext = dataContext;
            _operation = operation;
        }

        public void Update(Object entity)
        {
            switch (_operation.Method)
            {
                case ODataConstants.MethodDelete:
                    _entitySetAdapter.RemoveEntity(DataContext, entity);
                    break;
                case ODataConstants.MethodPatch:
                    _entitySetAdapter.AttachEntity(DataContext, entity);
                    break;
                case ODataConstants.MethodPost:
                    _entitySetAdapter.AddEntity(DataContext, entity);
                    break;
                default:
                    throw new NotImplementedException(_operation.Method);
            }
        }

        public Object DataContext => _dataContext;
        public String HttpMethod => _operation?.Method;
    }
}
