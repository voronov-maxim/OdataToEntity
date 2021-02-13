using LinqToDB;
using LinqToDB.Data;
using LinqToDB.Mapping;
using Microsoft.OData;
using System;
using System.Linq;
using System.Reflection;

namespace OdataToEntity.Linq2Db
{
    internal sealed class OeLinq2DbSetAdapter<T, TEntity> : Db.OeEntitySetAdapter where T : DataConnection, IOeLinq2DbDataContext where TEntity : class
    {
        private readonly Func<T, ITable<TEntity>> _getEntitySet;

        public OeLinq2DbSetAdapter(Func<T, ITable<TEntity>> getEntitySet, String entitySetName, bool isDbQuery)
        {
            _getEntitySet = getEntitySet;
            EntitySetName = entitySetName;
            IsDbQuery = isDbQuery;
        }

        public override void AddEntity(Object dataContext, ODataResourceBase entry)
        {
            var entity = (TEntity)OeEdmClrHelper.CreateEntity(EntityType, entry);
            AddInstanceAnnotation(entry, entity);
            GetTable(dataContext).Insert(entity);
        }
        private static void AddInstanceAnnotation(ODataResourceBase entry, TEntity entity)
        {
            var odataValue = new Infrastructure.OeOdataValue<TEntity>(entity);
            entry.InstanceAnnotations.Add(new ODataInstanceAnnotation("linq2db.EntityEntryValue", odataValue));
        }
        public override void AttachEntity(Object dataContext, ODataResourceBase entry)
        {
            var entity = (TEntity)OeEdmClrHelper.CreateEntity(EntityType, entry);
            GetTable(dataContext).Update(entity, entry.Properties.Select(p => p.Name));
        }
        private static OeLinq2DbTable<TEntity> GetTable(Object dataContext)
        {
            if (dataContext is IOeLinq2DbDataContext dc)
                return (OeLinq2DbTable<TEntity>)dc.DataContext.GetTable<TEntity>();

            throw new InvalidOperationException(dataContext.GetType().ToString() + "must implement " + nameof(IOeLinq2DbDataContext));
        }
        public override IQueryable GetEntitySet(Object dataContext)
        {
            return _getEntitySet((T)dataContext);
        }
        public override void RemoveEntity(Object dataContext, ODataResourceBase entry)
        {
            var entity = (TEntity)OeEdmClrHelper.CreateEntity(EntityType, entry);
            GetTable(dataContext).Delete(entity);
        }
        public override void UpdateEntityAfterSave(Object dataContext, ODataResourceBase resource)
        {
            foreach (ODataInstanceAnnotation instanceAnnotation in resource.InstanceAnnotations)
                if (instanceAnnotation.Value is Infrastructure.OeOdataValue<TEntity> entityEntry)
                {
                    foreach (ODataProperty odataProperty in resource.Properties)
                    {
                        PropertyInfo propertyInfo = typeof(TEntity).GetProperty(odataProperty.Name)!;
                        if (propertyInfo.IsDefined(typeof(IdentityAttribute)))
                        {
                            Object? value = propertyInfo.GetValue(entityEntry.Value);
                            odataProperty.Value = OeEdmClrHelper.CreateODataValue(value);
                        }
                    }
                    resource.InstanceAnnotations.Remove(instanceAnnotation);
                    break;
                }
        }

        public override Type EntityType => typeof(TEntity);
        public override String EntitySetName { get; }
        public override bool IsDbQuery { get; }
    }
}
