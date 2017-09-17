using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore.Extensions.Internal;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.OData.Edm;
using OdataToEntity.Parsers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity.EfCore
{
    internal interface IFromSql
    {
        IQueryable FromSql(Object dataContext, String sql, Object[] parameters);
    }

    public class OeEfCoreDataAdapter<T> : Db.OeDataAdapter where T : DbContext
    {
        private sealed class DbSetAdapterImpl<TEntity> : Db.OeEntitySetMetaAdapter, IFromSql where TEntity : class
        {
            private readonly String _entitySetName;
            private readonly Func<T, DbSet<TEntity>> _getEntitySet;
            private IKey _key;
            private IClrPropertyGetter[] _keyGetters;
            private IProperty[] _properties;
            private IForeignKey _selfReferenceKey;

            public DbSetAdapterImpl(Func<T, DbSet<TEntity>> getEntitySet, String entitySetName)
            {
                _getEntitySet = getEntitySet;
                _entitySetName = entitySetName;
            }

            public override void AddEntity(Object dataContext, Object entity)
            {
                var context = (T)dataContext;
                DbSet<TEntity> dbSet = _getEntitySet(context);
                EntityEntry<TEntity> entry = dbSet.Add((TEntity)entity);

                InitKey(context);
                for (int i = 0; i < _key.Properties.Count; i++)
                {
                    IProperty property = _key.Properties[i];
                    if (property.ValueGenerated == ValueGenerated.OnAdd)
                        entry.GetInfrastructure().MarkAsTemporary(property);
                }
            }
            public override void AttachEntity(Object dataContext, Object entity)
            {
                AttachEntity(dataContext, entity, EntityState.Modified);
            }
            private void AttachEntity(Object dataContext, Object entity, EntityState entityState)
            {
                var context = (T)dataContext;
                InternalEntityEntry internalEntry = GetEntityEntry(context, entity);
                if (internalEntry == null)
                {
                    DbSet<TEntity> dbSet = _getEntitySet(context);
                    dbSet.Attach((TEntity)entity);
                    context.Entry(entity).State = entityState;
                }
                else
                {
                    if (entityState == EntityState.Modified)
                        foreach (IProperty property in _properties)
                        {
                            Object value = property.GetGetter().GetClrValue(entity);
                            internalEntry.SetCurrentValue(property, value);
                        }
                    else
                        internalEntry.SetEntityState(entityState);
                }
            }
            public IQueryable FromSql(Object dataContext, String sql, Object[] parameters)
            {
                DbSet<TEntity> dbSet = _getEntitySet((T)dataContext);
                return dbSet.FromSql<TEntity>(sql, parameters);
            }
            public override IQueryable GetEntitySet(Object dataContext)
            {
                return _getEntitySet((T)dataContext);
            }
            private InternalEntityEntry GetEntityEntry(T context, Object entity)
            {
                InitKey(context);
                var buffer = new ValueBuffer(GetKeyValues((TEntity)entity));
                var stateManager = (IInfrastructure<IStateManager>)context.ChangeTracker;
                return stateManager.Instance.TryGetEntry(_key, buffer, false);
            }
            private Object[] GetKeyValues(TEntity entity)
            {
                var keyValues = new Object[_keyGetters.Length];
                for (int i = 0; i < keyValues.Length; i++)
                    keyValues[i] = _keyGetters[i].GetClrValue(entity);
                return keyValues;
            }
            public override void RemoveEntity(Object dataContext, Object entity)
            {
                var context = (T)dataContext;
                InternalEntityEntry internalEntry = GetEntityEntry(context, entity);
                if (internalEntry == null)
                {
                    DbSet<TEntity> dbSet = _getEntitySet(context);
                    if (_selfReferenceKey == null)
                        dbSet.Attach((TEntity)entity);
                    else
                        entity = dbSet.Find(GetKeyValues((TEntity)entity));
                    context.Entry(entity).State = EntityState.Deleted;
                }
                else
                    internalEntry.SetEntityState(EntityState.Deleted);
            }
            private void InitKey(T context)
            {
                if (_keyGetters == null)
                {
                    IEntityType entityType = context.Model.FindEntityType(EntityType);
                    foreach (IForeignKey fkey in entityType.GetForeignKeys())
                        if (fkey.IsSelfReferencing())
                        {
                            Volatile.Write(ref _selfReferenceKey, fkey);
                            break;
                        }

                    Volatile.Write(ref _key, entityType.FindPrimaryKey());
                    Volatile.Write(ref _properties, entityType.GetProperties().Where(p => !p.IsPrimaryKey()).ToArray());
                    Volatile.Write(ref _keyGetters, _key.Properties.Select(k => k.GetGetter()).ToArray());
                }
            }

            public override Type EntityType
            {
                get
                {
                    return typeof(TEntity);
                }
            }
            public override String EntitySetName
            {
                get
                {
                    return _entitySetName;
                }
            }
        }

        private readonly static Db.OeEntitySetMetaAdapterCollection _entitySetMetaAdapters = CreateEntitySetMetaAdapters();
        private readonly DbContextPool<T> _dbContextPool;
        private readonly OeEfCoreOperationAdapter _operationAdapter;

        public OeEfCoreDataAdapter() : this(null, null)
        {
        }
        public OeEfCoreDataAdapter(DbContextOptions options, Db.OeQueryCache queryCache)
            : base(queryCache)
        {
            if (options != null)
                _dbContextPool = new DbContextPool<T>(options);

            _operationAdapter = new OeEfCoreOperationAdapter(typeof(T), _entitySetMetaAdapters);
        }

        public override void CloseDataContext(Object dataContext)
        {
            var dbContext = (T)dataContext;
            if (_dbContextPool == null)
                dbContext.Dispose();
            else
                _dbContextPool.Return(dbContext);
        }
        public override Object CreateDataContext()
        {
            T dbContext;
            if (_dbContextPool == null)
                dbContext = Db.FastActivator.CreateInstance<T>();
            else
                dbContext = _dbContextPool.Rent();

            dbContext.ChangeTracker.AutoDetectChangesEnabled = false;
            dbContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
            return dbContext;
        }
        private static Db.OeEntitySetMetaAdapterCollection CreateEntitySetMetaAdapters()
        {
            var entitySetMetaAdapters = new List<Db.OeEntitySetMetaAdapter>();
            foreach (PropertyInfo property in typeof(T).GetTypeInfo().GetProperties())
            {
                Type dbSetType = property.PropertyType.GetTypeInfo().GetInterface(typeof(IQueryable<>).FullName);
                if (dbSetType != null)
                    entitySetMetaAdapters.Add(CreateDbSetInvoker(property, dbSetType));
            }

            return new Db.OeEntitySetMetaAdapterCollection(entitySetMetaAdapters.ToArray(), new ModelBuilder.OeEdmModelMetadataProvider());
        }
        private static Db.OeEntitySetMetaAdapter CreateDbSetInvoker(PropertyInfo property, Type dbSetType)
        {
            MethodInfo mi = ((Func<PropertyInfo, Db.OeEntitySetMetaAdapter>)CreateDbSetInvoker<Object>).GetMethodInfo().GetGenericMethodDefinition();
            Type entityType = dbSetType.GetTypeInfo().GetGenericArguments()[0];
            MethodInfo func = mi.GetGenericMethodDefinition().MakeGenericMethod(entityType);
            return (Db.OeEntitySetMetaAdapter)func.Invoke(null, new Object[] { property });
        }
        private static Db.OeEntitySetMetaAdapter CreateDbSetInvoker<TEntity>(PropertyInfo property) where TEntity : class
        {
            var getDbSet = (Func<T, DbSet<TEntity>>)property.GetGetMethod().CreateDelegate(typeof(Func<T, DbSet<TEntity>>));
            return new DbSetAdapterImpl<TEntity>(getDbSet, property.Name);
        }
        public override Db.OeAsyncEnumerator ExecuteEnumerator(Object dataContext, OeParseUriContext parseUriContext, CancellationToken cancellationToken)
        {
            IAsyncEnumerable<Object> asyncEnumerable;
            if (base.QueryCache.AllowCache)
                asyncEnumerable = GetFromCache<Object>(parseUriContext, (T)dataContext, base.QueryCache);
            else
                asyncEnumerable = ((IQueryable<Object>)base.CreateQuery(parseUriContext, dataContext, new OeConstantToVariableVisitor())).AsAsyncEnumerable();

            Db.OeAsyncEnumerator asyncEnumerator = new Db.OeAsyncEnumerator(asyncEnumerable.GetEnumerator(), cancellationToken);
            if (parseUriContext.CountExpression != null)
            {
                IQueryable query = parseUriContext.EntitySetAdapter.GetEntitySet(dataContext);
                asyncEnumerator.Count = query.Provider.Execute<int>(parseUriContext.CountExpression);
            }

            return asyncEnumerator;
        }
        public override TResult ExecuteScalar<TResult>(Object dataContext, OeParseUriContext parseUriContext)
        {
            if (base.QueryCache.AllowCache)
                return GetFromCache<TResult>(parseUriContext, (T)dataContext, base.QueryCache).Single().GetAwaiter().GetResult();

            IQueryable query = parseUriContext.EntitySetAdapter.GetEntitySet(dataContext);
            Expression expression = parseUriContext.CreateExpression(query, new OeConstantToVariableVisitor());
            return query.Provider.Execute<TResult>(expression);
        }
        public override Db.OeEntitySetAdapter GetEntitySetAdapter(String entitySetName)
        {
            return new Db.OeEntitySetAdapter(_entitySetMetaAdapters.FindByEntitySetName(entitySetName), this);
        }
        private static IAsyncEnumerable<TResult> GetFromCache<TResult>(OeParseUriContext parseUriContext, T dbContext, Db.OeQueryCache queryCache)
        {
            Db.QueryCacheItem queryCacheItem = queryCache.GetQuery(parseUriContext);

            Func<QueryContext, IAsyncEnumerable<TResult>> queryExecutor;
            Expression countExpression;
            if (queryCacheItem == null)
            {
                IQueryable query = parseUriContext.EntitySetAdapter.GetEntitySet(dbContext);
                var parameterVisitor = new OeConstantToParameterVisitor();

                Expression expression = parseUriContext.CreateExpression(query, parameterVisitor);
                queryExecutor = dbContext.CreateAsyncQueryExecutor<TResult>(expression);
                countExpression = parseUriContext.CreateCountExpression(query, expression);
                queryCache.AddQuery(parseUriContext, queryExecutor, countExpression, parameterVisitor.ConstantToParameterMapper);
                parseUriContext.ParameterValues = parameterVisitor.ParameterValues;
            }
            else
            {
                queryExecutor = (Func<QueryContext, IAsyncEnumerable<TResult>>)queryCacheItem.Query;
                parseUriContext.EntryFactory = queryCacheItem.EntryFactory;
                countExpression = queryCacheItem.CountExpression;
            }

            var queryContextFactory = dbContext.GetService<IQueryContextFactory>();
            QueryContext queryContext = queryContextFactory.Create();
            foreach (Db.OeQueryCacheDbParameterValue parameterValue in parseUriContext.ParameterValues)
                queryContext.AddParameter(parameterValue.ParameterName, parameterValue.ParameterValue);

            if (parseUriContext.ODataUri.QueryCount.GetValueOrDefault())
                parseUriContext.CountExpression = new OeParameterToVariableVisitor().Translate(countExpression, parseUriContext.ParameterValues);

            return queryExecutor(queryContext);
        }
        private static IAsyncEnumerable<TResult> GetQueryExecutor<TResult>(OeParseUriContext parseUriContext, T dbContext)
        {
            IQueryable query = parseUriContext.EntitySetAdapter.GetEntitySet(dbContext);

            var parameterVisitor = new OeConstantToParameterVisitor();
            Expression expression = parseUriContext.CreateExpression(query, parameterVisitor);
            Func<QueryContext, IAsyncEnumerable<TResult>> queryExecutor = dbContext.CreateAsyncQueryExecutor<TResult>(expression);
            parseUriContext.ParameterValues = parameterVisitor.ParameterValues;

            var queryContextFactory = dbContext.GetService<IQueryContextFactory>();
            QueryContext queryContext = queryContextFactory.Create();
            foreach (Db.OeQueryCacheDbParameterValue parameterValue in parseUriContext.ParameterValues)
                queryContext.AddParameter(parameterValue.ParameterName, parameterValue.ParameterValue);

            if (parseUriContext.ODataUri.QueryCount.GetValueOrDefault())
            {
                Expression countExpression = parseUriContext.CreateCountExpression(query, expression);
                parseUriContext.CountExpression = new OeParameterToVariableVisitor().Translate(countExpression, parseUriContext.ParameterValues);
            }

            return queryExecutor(queryContext);
        }
        public override Task<int> SaveChangesAsync(IEdmModel edmModel, Object dataContext, CancellationToken cancellationToken)
        {
            var dbContext = (T)dataContext;
            return dbContext.SaveChangesAsync(cancellationToken);
        }

        public override Db.OeEntitySetMetaAdapterCollection EntitySetMetaAdapters => _entitySetMetaAdapters;
        public override Db.OeOperationAdapter OperationAdapter => _operationAdapter;
    }
}
