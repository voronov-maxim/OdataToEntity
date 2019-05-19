using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.OData;

namespace OdataToEntity.EfCore.DynamicDataContext
{
    public class DynamicEntitySetAdapter : Db.OeEntitySetAdapter
    {
        private readonly DynamicTypeDefinition _typeDefinition;

        public DynamicEntitySetAdapter(DynamicTypeDefinition typeDefinition)
        {
            _typeDefinition = typeDefinition;
        }

        public override void AddEntity(Object dataContext, ODataResourceBase entry)
        {
            throw new NotImplementedException();
        }
        public override void AttachEntity(Object dataContext, ODataResourceBase entry)
        {
            throw new NotImplementedException();
        }
        public override IQueryable GetEntitySet(Object dataContext)
        {
            var dynamicDbContext = (DynamicDbContext)dataContext;
            return DynamicTypeDefinitionManager.GetQueryable(dynamicDbContext, _typeDefinition.DynamicTypeType);
        }
        public override void RemoveEntity(Object dataContext, ODataResourceBase entry)
        {
            throw new NotImplementedException();
        }

        public override Type EntityType => _typeDefinition.DynamicTypeType;
        public override String EntitySetName => _typeDefinition.TableName;
    }
}
