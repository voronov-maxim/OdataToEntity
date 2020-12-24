using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.OData;
using System;
using System.Linq;

namespace OdataToEntity.EfCore
{
    internal sealed class OeDbQueryAdapter : Db.OeEntitySetAdapter, IFromSql
    {
        private readonly Type _clrEntityType;

        public OeDbQueryAdapter(Type clrEntityType, String entitySetName)
        {
            _clrEntityType = clrEntityType;
            EntitySetName = entitySetName;
        }

        public override void AddEntity(Object dataContext, ODataResourceBase entry)
        {
            throw new NotSupportedException();
        }
        public override void AttachEntity(Object dataContext, ODataResourceBase entry)
        {
            throw new NotSupportedException();
        }
        public IQueryable FromSql(Object dataContext, String sql, Object?[] parameters)
        {
            var dbContext = (DbContext)dataContext;
            IEntityType entityType = dbContext.Model.FindEntityType(EntityType);
            return OeDbSetAdapter.FromSql(dataContext, EntityType, entityType, sql, parameters);
        }
        public override IQueryable GetEntitySet(Object dataContext)
        {
            return OeDbSetAdapter.GetEntitySet(dataContext, EntityType);
        }
        public override void RemoveEntity(Object dataContext, ODataResourceBase entry)
        {
            throw new NotSupportedException();
        }

        public override Type EntityType => _clrEntityType;
        public override String EntitySetName { get; }
        public override bool IsDbQuery => true;
    }
}
