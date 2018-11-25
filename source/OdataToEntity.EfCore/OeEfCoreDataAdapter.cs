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
using Microsoft.OData;
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
        private sealed class DbSetAdapterImpl<TEntity> : Db.OeEntitySetAdapter, IFromSql where TEntity : class
        {
            private IEntityType _entityType;
            private readonly Func<T, DbSet<TEntity>> _getEntitySet;
            private Func<MaterializationContext, Object> _materializer;
            private Func<Object[]> _valueBufferArrayInit;
            private IForeignKey _selfReferenceKey;

            public DbSetAdapterImpl(Func<T, DbSet<TEntity>> getEntitySet, String entitySetName, String dataContextFullName)
            {
                _getEntitySet = getEntitySet;
                EntitySetName = entitySetName;
                EntitySetFullName = dataContextFullName + "." + entitySetName;
            }

            public override void AddEntity(Object dataContext, ODataResourceBase entry)
            {
                var context = (T)dataContext;
                DbSet<TEntity> dbSet = _getEntitySet(context);
                EntityEntry<TEntity> entityEntry = dbSet.Add(CreateEntity(context, entry));

                IReadOnlyList<IProperty> keyProperties = _entityType.FindPrimaryKey().Properties;
                for (int i = 0; i < keyProperties.Count; i++)
                    if (keyProperties[i].ValueGenerated == ValueGenerated.OnAdd)
                        entityEntry.GetInfrastructure().MarkAsTemporary(keyProperties[i]);
            }
            public override void AttachEntity(Object dataContext, ODataResourceBase entry)
            {
                var context = (T)dataContext;
                InternalEntityEntry internalEntry = GetEntityEntry(context, entry);
                if (internalEntry == null)
                {
                    TEntity entity = CreateEntity(context, entry);
                    _getEntitySet(context).Attach(entity);
                    internalEntry = _getEntitySet(context).Attach(entity).GetInfrastructure();

                    IKey key = _entityType.FindPrimaryKey();
                    foreach (ODataProperty odataProperty in entry.Properties)
                    {
                        IProperty property = _entityType.FindProperty(odataProperty.Name);
                        if (!key.Properties.Contains(property))
                            internalEntry.SetPropertyModified(property);
                    }
                }
                else
                {
                    foreach (ODataProperty odataProperty in entry.Properties)
                    {
                        IProperty property = _entityType.FindProperty(odataProperty.Name);
                        Object value = OeEdmClrHelper.GetClrValue(property.ClrType, odataProperty.Value);
                        internalEntry.SetProperty(property, value);
                    }
                }
            }
            private TEntity CreateEntity(DbContext context, ODataResourceBase entry)
            {
                Initialize(context);

                var values = _valueBufferArrayInit();
                foreach (ODataProperty odataProperty in entry.Properties)
                {
                    IProperty property = _entityType.FindProperty(odataProperty.Name);
                    Object value = OeEdmClrHelper.GetClrValue(property.ClrType, odataProperty.Value);
                    values[property.GetIndex()] = value;
                }
                return (TEntity)_materializer(new MaterializationContext(new ValueBuffer(values), context));
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
            public IQueryable FromSql(Object dataContext, String sql, Object[] parameters)
            {
                DbSet<TEntity> dbSet = _getEntitySet((T)dataContext);
                return dbSet.FromSql(sql, parameters);
            }
            public override IQueryable GetEntitySet(Object dataContext)
            {
                return _getEntitySet((T)dataContext);
            }
            private InternalEntityEntry GetEntityEntry(T context, ODataResourceBase entity)
            {
                Initialize(context);
                var buffer = new ValueBuffer(GetKeyValues(entity));
                var stateManager = (IInfrastructure<IStateManager>)context.ChangeTracker;
                return stateManager.Instance.TryGetEntry(_entityType.FindPrimaryKey(), buffer, false);
            }
            private Object[] GetKeyValues(ODataResourceBase entity)
            {
                IKey key = _entityType.FindPrimaryKey();
                var keyValues = new Object[key.Properties.Count];
                for (int i = 0; i < keyValues.Length; i++)
                {
                    String keyName = key.Properties[i].Name;
                    foreach (ODataProperty odataProperty in entity.Properties)
                        if (String.Compare(odataProperty.Name, keyName, StringComparison.OrdinalIgnoreCase) == 0)
                        {
                            keyValues[i] = odataProperty.Value;
                            break;
                        }
                }
                return keyValues;
            }
            public override void RemoveEntity(Object dataContext, ODataResourceBase entry)
            {
                var context = (T)dataContext;
                InternalEntityEntry entityEntry = GetEntityEntry(context, entry);
                if (entityEntry == null)
                {
                    TEntity entity = CreateEntity(context, entry);
                    DbSet<TEntity> dbSet = _getEntitySet(context);
                    if (_selfReferenceKey == null)
                        dbSet.Attach(entity);
                    else
                        entity = dbSet.Find(GetKeyValues(entry));
                    context.Entry(entity).State = EntityState.Deleted;
                }
                else
                    entityEntry.SetEntityState(EntityState.Deleted);
            }
            private void Initialize(DbContext context)
            {
                if (_entityType == null)
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

            public override Type EntityType => typeof(TEntity);
            public override String EntitySetFullName { get; }
            public override String EntitySetName { get; }
        }

        private readonly DbContextPool<T> _dbContextPool;
        protected readonly static Db.OeEntitySetAdapterCollection _entitySetAdapters = CreateEntitySetAdapters();

        public OeEfCoreDataAdapter() : this(null, null)
        {
        }
        public OeEfCoreDataAdapter(DbContextOptions options) : this(options, null)
        {
        }
        public OeEfCoreDataAdapter(Cache.OeQueryCache queryCache) : this(null, queryCache)
        {
        }
        public OeEfCoreDataAdapter(DbContextOptions options, Cache.OeQueryCache queryCache)
            : this(options, queryCache, new OeEfCoreOperationAdapter(typeof(T), _entitySetAdapters))
        {
        }
        public OeEfCoreDataAdapter(DbContextOptions options, Cache.OeQueryCache queryCache, OeEfCoreOperationAdapter operationAdapter)
            : base(queryCache, operationAdapter)
        {
            if (options != null)
                _dbContextPool = new DbContextPool<T>(options);
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
                dbContext = Infrastructure.FastActivator.CreateInstance<T>();
            else
                dbContext = _dbContextPool.Rent();

            dbContext.ChangeTracker.AutoDetectChangesEnabled = false;
            dbContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
            return dbContext;
        }
        private static Db.OeEntitySetAdapterCollection CreateEntitySetAdapters()
        {
            var entitySetAdapters = new List<Db.OeEntitySetAdapter>();
            foreach (PropertyInfo property in typeof(T).GetProperties())
            {
                Type dbSetType = property.PropertyType.GetInterface(typeof(IQueryable<>).FullName);
                if (dbSetType != null)
                    entitySetAdapters.Add(CreateEntitySetAdapter(property, dbSetType));
            }

            return new Db.OeEntitySetAdapterCollection(entitySetAdapters.ToArray());
        }
        private static Db.OeEntitySetAdapter CreateEntitySetAdapter(PropertyInfo property, Type dbSetType)
        {
            MethodInfo mi = ((Func<PropertyInfo, Db.OeEntitySetAdapter>)CreateDbSetInvoker<Object>).GetMethodInfo().GetGenericMethodDefinition();
            Type entityType = dbSetType.GetGenericArguments()[0];
            MethodInfo func = mi.GetGenericMethodDefinition().MakeGenericMethod(entityType);
            return (Db.OeEntitySetAdapter)func.Invoke(null, new Object[] { property });
        }
        private static Db.OeEntitySetAdapter CreateDbSetInvoker<TEntity>(PropertyInfo property) where TEntity : class
        {
            var getDbSet = (Func<T, DbSet<TEntity>>)property.GetGetMethod().CreateDelegate(typeof(Func<T, DbSet<TEntity>>));
            return new DbSetAdapterImpl<TEntity>(getDbSet, property.Name, typeof(T).FullName);
        }
        public override Db.OeAsyncEnumerator ExecuteEnumerator(Object dataContext, OeQueryContext queryContext, CancellationToken cancellationToken)
        {
            IAsyncEnumerable<Object> asyncEnumerable;
            MethodCallExpression countExpression = null;
            if (base.QueryCache.AllowCache)
                asyncEnumerable = GetFromCache<Object>(queryContext, (T)dataContext, base.QueryCache, out countExpression);
            else
            {
                Expression expression = queryContext.CreateExpression(new OeConstantToVariableVisitor());
                IQueryable entitySet = queryContext.EntitySetAdapter.GetEntitySet(dataContext);
                IQueryable query = entitySet.Provider.CreateQuery(queryContext.TranslateSource(dataContext, expression));
                asyncEnumerable = ((IQueryable<Object>)query).AsAsyncEnumerable();

                if (queryContext.ODataUri.QueryCount.GetValueOrDefault())
                    countExpression = OeQueryContext.CreateCountExpression(query.Expression);
            }

            Db.OeAsyncEnumerator asyncEnumerator = new OeEfCoreAsyncEnumerator(asyncEnumerable.GetEnumerator(), cancellationToken);
            if (countExpression != null)
            {
                IQueryable query = queryContext.EntitySetAdapter.GetEntitySet(dataContext);
                asyncEnumerator.Count = query.Provider.Execute<int>(countExpression);
            }

            return asyncEnumerator;
        }
        public override TResult ExecuteScalar<TResult>(Object dataContext, OeQueryContext queryContext)
        {
            if (base.QueryCache.AllowCache)
                return GetFromCache<TResult>(queryContext, (T)dataContext, base.QueryCache, out _).Single().GetAwaiter().GetResult();

            IQueryable query = queryContext.EntitySetAdapter.GetEntitySet(dataContext);
            Expression expression = queryContext.CreateExpression(new OeConstantToVariableVisitor());
            return query.Provider.Execute<TResult>(queryContext.TranslateSource(dataContext, expression));
        }
        private static IAsyncEnumerable<TResult> GetFromCache<TResult>(OeQueryContext queryContext, T dbContext, Cache.OeQueryCache queryCache,
            out MethodCallExpression countExpression)
        {
            countExpression = null;
            Cache.OeCacheContext cacheContext = queryContext.CreateCacheContext();
            Cache.OeQueryCacheItem queryCacheItem = queryCache.GetQuery(cacheContext);

            Func<QueryContext, IAsyncEnumerable<TResult>> queryExecutor;
            IReadOnlyList<Cache.OeQueryCacheDbParameterValue> parameterValues;
            IQueryable query = queryContext.EntitySetAdapter.GetEntitySet(dbContext);
            if (queryCacheItem == null)
            {
                var parameterVisitor = new OeConstantToParameterVisitor();
                Expression expression = queryContext.CreateExpression(parameterVisitor);
                expression = queryContext.TranslateSource(dbContext, expression);

                queryExecutor = dbContext.CreateAsyncQueryExecutor<TResult>(expression);
                countExpression = OeQueryContext.CreateCountExpression(expression);
                queryCache.AddQuery(queryContext.CreateCacheContext(parameterVisitor.ConstantToParameterMapper), queryExecutor, countExpression,
                    queryContext.EntryFactory, queryContext.SkipTokenAccessors);
                parameterValues = parameterVisitor.ParameterValues;
            }
            else
            {
                queryExecutor = (Func<QueryContext, IAsyncEnumerable<TResult>>)queryCacheItem.Query;
                queryContext.EntryFactory = queryCacheItem.EntryFactory;
                queryContext.SkipTokenAccessors = queryCacheItem.SkipTokenAccessors;
                countExpression = queryCacheItem.CountExpression;
                parameterValues = cacheContext.ParameterValues;
            }

            var queryContextFactory = dbContext.GetService<IQueryContextFactory>();
            QueryContext efQueryContext = queryContextFactory.Create();
            foreach (Cache.OeQueryCacheDbParameterValue parameterValue in parameterValues)
                efQueryContext.AddParameter(parameterValue.ParameterName, parameterValue.ParameterValue);

            if (queryContext.ODataUri.QueryCount.GetValueOrDefault())
            {
                countExpression = (MethodCallExpression)queryContext.TranslateSource(dbContext, countExpression);
                countExpression = (MethodCallExpression)new OeParameterToVariableVisitor().Translate(countExpression, parameterValues);
            }
            else
                countExpression = null;

            return queryExecutor(efQueryContext);
        }
        public override Task<int> SaveChangesAsync(IEdmModel edmModel, Object dataContext, CancellationToken cancellationToken)
        {
            var dbContext = (T)dataContext;
            return dbContext.SaveChangesAsync(cancellationToken);
        }

        public override Type DataContextType => typeof(T);
        public sealed override Db.OeEntitySetAdapterCollection EntitySetAdapters => _entitySetAdapters;
    }
}
