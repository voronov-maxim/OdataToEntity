using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.OData;
using OdataToEntity.Parsers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;

namespace OdataToEntity.EfCore
{
    internal sealed class OeDbSetAdapter : Db.OeEntitySetAdapter, IFromSql
    {
        private readonly Type _clrEntityType;
        private IEntityType _entityType;
        private Func<MaterializationContext, Object> _materializer;
        private static readonly IEntityType _nullEntityType = new EntityType("dummy", new Model(), ConfigurationSource.Convention);
        private Func<Object[]> _valueBufferArrayInit;
        private IForeignKey? _selfReferenceKey;

        public OeDbSetAdapter(Type clrEntityType, String entitySetName)
        {
            _clrEntityType = clrEntityType;
            EntitySetName = entitySetName;
            _entityType = _nullEntityType;
            _materializer = NullMaterializer;
            _valueBufferArrayInit = NullValueBufferArrayInit;
        }

        public override void AddEntity(Object dataContext, ODataResourceBase entry)
        {
            var context = (DbContext)dataContext;
            EntityEntry entityEntry = context.Add(CreateEntity(context, entry));
            AddInstanceAnnotation(entry, entityEntry);

            IReadOnlyList<IProperty> keyProperties = _entityType.FindPrimaryKey().Properties;
            for (int i = 0; i < keyProperties.Count; i++)
                if (keyProperties[i].ValueGenerated == ValueGenerated.OnAdd)
                    entityEntry.Property(keyProperties[i].Name).IsTemporary = true;
        }
        private static void AddInstanceAnnotation(ODataResourceBase entry, EntityEntry entityEntry)
        {
            var odataValue = new Infrastructure.OeOdataValue<EntityEntry>(entityEntry);
            entry.InstanceAnnotations.Add(new ODataInstanceAnnotation("ef.EntityEntryValue", odataValue));
        }
        public override void AttachEntity(Object dataContext, ODataResourceBase entry)
        {
            var dbContext = (DbContext)dataContext;
            InternalEntityEntry internalEntry = GetEntityEntry(dbContext, entry);
            if (internalEntry == null)
            {
                Object entity = CreateEntity(dbContext, entry);
                EntityEntry entityEntry = dbContext.Attach(entity);
                AddInstanceAnnotation(entry, entityEntry);

                IReadOnlyList<IProperty> keyProperties = _entityType.FindPrimaryKey().Properties;
                foreach (ODataProperty odataProperty in entry.Properties)
                {
                    IProperty property = _entityType.FindProperty(odataProperty.Name);
                    if (!keyProperties.Contains(property))
                        entityEntry.Property(property.Name).IsModified = true;
                }
            }
            else
            {
                foreach (ODataProperty odataProperty in entry.Properties)
                {
                    IProperty property = _entityType.FindProperty(odataProperty.Name);
                    Object value = OeEdmClrHelper.GetClrValue(property.ClrType, odataProperty.Value);
                    internalEntry.SetProperty(property, value, false);
                }
            }
        }
        private Object CreateEntity(DbContext context, ODataResourceBase entry)
        {
            Initialize(context);

            var values = _valueBufferArrayInit();
            foreach (ODataProperty odataProperty in entry.Properties)
            {
                IProperty property = _entityType.FindProperty(odataProperty.Name);
                Object value = OeEdmClrHelper.GetClrValue(property.ClrType, odataProperty.Value);
                values[property.GetIndex()] = value;
            }
            return _materializer(new MaterializationContext(new ValueBuffer(values), context));
        }
        private static Func<Object[]> CreateNewArrayInit(IEntityType entityType)
        {
            var constants = new Expression[entityType.PropertyCount()];
            foreach (IProperty property in entityType.GetProperties())
                constants[property.GetIndex()] = property.ClrType.IsValueType ?
                    Expression.Convert(Expression.Constant(Activator.CreateInstance(property.ClrType)), typeof(Object)) :
                    (Expression)OeConstantToVariableVisitor.NullConstantExpression;

            NewArrayExpression newArrayExpression = Expression.NewArrayInit(typeof(Object), constants);
            return Expression.Lambda<Func<Object[]>>(newArrayExpression).Compile();
        }
        public IQueryable FromSql(Object dataContext, String sql, Object?[] parameters)
        {
            return FromSql(dataContext, EntityType, _entityType, sql, parameters);
        }
        internal static IQueryable FromSql(Object dataContext, Type clrEntityType, IEntityType entityType, String sql, Object?[] parameters)
        {
            IQueryable queryable = GetEntitySet(dataContext, clrEntityType);
            var dbContextDependencies = (IDbContextDependencies)dataContext;
            var expression = new FromSqlQueryRootExpression(dbContextDependencies.QueryProvider, entityType, sql, Expression.Constant(parameters));
            return queryable.Provider.CreateQuery(expression);
        }
        public override IQueryable GetEntitySet(Object dataContext)
        {
            return GetEntitySet(dataContext, EntityType);
        }
        internal static IQueryable GetEntitySet(Object dataContext, Type entityType)
        {
            var dbSetCache = (IDbSetCache)dataContext;
            var dbContextDependencies = (IDbContextDependencies)dataContext;
            return (IQueryable)dbSetCache.GetOrAddSet(dbContextDependencies.SetSource, entityType);
        }
        private InternalEntityEntry GetEntityEntry(DbContext context, ODataResourceBase entity)
        {
            Initialize(context);
            IStateManager stateManager = ((IDbContextDependencies)context).StateManager;
            return stateManager.TryGetEntry(_entityType.FindPrimaryKey(), GetKeyValues(entity));
        }
        private Object[] GetKeyValues(ODataResourceBase entity)
        {
            IReadOnlyList<IProperty> keyProperties = _entityType.FindPrimaryKey().Properties;
            var keyValues = new Object[keyProperties.Count];
            for (int i = 0; i < keyValues.Length; i++)
            {
                String keyName = keyProperties[i].Name;
                foreach (ODataProperty odataProperty in entity.Properties)
                    if (String.Compare(odataProperty.Name, keyName, StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        keyValues[i] = odataProperty.Value;
                        break;
                    }
            }
            return keyValues;
        }
        private void Initialize(DbContext context)
        {
            if (_entityType == _nullEntityType)
            {
                IEntityType entityType = context.Model.FindEntityType(EntityType);
                foreach (IForeignKey fkey in entityType.GetForeignKeys())
                    if (fkey.IsSelfReferencing())
                    {
                        Volatile.Write(ref _selfReferenceKey, fkey);
                        break;
                    }

                Volatile.Write(ref _valueBufferArrayInit, CreateNewArrayInit(entityType));

                var entityMaterializerSource = context.GetService<IEntityMaterializerSource>();
                Volatile.Write(ref _materializer, entityMaterializerSource.GetMaterializer(entityType));

                Volatile.Write(ref _entityType, entityType);
            }
        }
        private static Object NullMaterializer(MaterializationContext materializationContext)
        {
            throw new InvalidOperationException("Stub for nullable reference type");
        }
        private static Object[] NullValueBufferArrayInit()
        {
            throw new InvalidOperationException("Stub for nullable reference type");
        }
        public override void RemoveEntity(Object dataContext, ODataResourceBase entry)
        {
            var dbContext = (DbContext)dataContext;
            InternalEntityEntry entityEntry = GetEntityEntry(dbContext, entry);
            if (entityEntry == null)
            {
                Object entity = CreateEntity(dbContext, entry);
                if (_selfReferenceKey == null)
                    dbContext.Attach(entity);
                else
                {
                    IEntityFinder finder = dbContext.GetDependencies().EntityFinderFactory.Create(_entityType);
                    entity = finder.Find(GetKeyValues(entry));
                }
                dbContext.Entry(entity).State = EntityState.Deleted;
            }
            else
                entityEntry.SetEntityState(EntityState.Deleted);
        }
        public override void UpdateEntityAfterSave(Object dataContext, ODataResourceBase resource)
        {
            foreach (ODataInstanceAnnotation instanceAnnotation in resource.InstanceAnnotations)
                if (instanceAnnotation.Value is Infrastructure.OeOdataValue<EntityEntry> entityEntry)
                {
                    PropertyValues propertyValues = entityEntry.Value.CurrentValues;
                    foreach (ODataProperty odataProperty in resource.Properties)
                    {
                        IProperty property = entityEntry.Value.Property(odataProperty.Name).Metadata;
                        if (property.ValueGenerated != ValueGenerated.Never || property.IsForeignKey())
                            odataProperty.Value = OeEdmClrHelper.CreateODataValue(propertyValues[property]);
                    }
                    resource.InstanceAnnotations.Remove(instanceAnnotation);
                    break;
                }
        }

        public override Type EntityType => _clrEntityType;
        public override String EntitySetName { get; }
    }
}
