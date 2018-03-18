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
        private sealed class DbSetAdapterImpl<TEntity> : Db.OeEntitySetMetaAdapter, IFromSql where TEntity : class
        {
            private IEntityType _entityType;
            private readonly Func<T, DbSet<TEntity>> _getEntitySet;
            private Func<ValueBuffer, Object> _materializer;
            private Func<Object[]> _valueBufferArrayInit;
            private IForeignKey _selfReferenceKey;

            public DbSetAdapterImpl(Func<T, DbSet<TEntity>> getEntitySet, String entitySetName)
            {
                _getEntitySet = getEntitySet;
                EntitySetName = entitySetName;
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
                        Object value = OeEdmClrHelper.GetStructuralValue(property.ClrType, odataProperty.Value);
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
                    Object value = OeEdmClrHelper.GetStructuralValue(property.ClrType, odataProperty.Value);
                    values[property.GetIndex()] = value;
                }
                return (TEntity)_materializer(new ValueBuffer(values));
            }
            private static Func<Object[]> CreateNewArrayInit(IEntityType entityType)
            {
                var constants = new Expression[entityType.PropertyCount()];
                foreach (IProperty property in entityType.GetProperties())
                    constants[property.GetIndex()] = property.ClrType.IsValueType ?
                        Expression.Convert(Expression.Constant(Activator.CreateInstance(property.ClrType)), typeof(Object)) :
                        (Expression)Expression.Constant(null);

                NewArrayExpression newArrayExpression = Expression.NewArrayInit(typeof(Object), constants);
                return Expression.Lambda<Func<Object[]>>(newArrayExpression).Compile();
            }
            public IQueryable FromSql(Object dataContext, String sql, Object[] parameters)
            {
                DbSet<TEntity> dbSet = _getEntitySet((T)dataContext);
                return dbSet.FromSql(sql, parameters);
            }
            public override IQueryable GetEntitySet(Object dataContext) => _getEntitySet((T)dataContext);
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
            public override String EntitySetName { get; }
        }

        private readonly DbContextPool<T> _dbContextPool;
        protected readonly static Db.OeEntitySetMetaAdapterCollection _entitySetMetaAdapters = CreateEntitySetMetaAdapters();

        public OeEfCoreDataAdapter() : this(null, null)
        {
        }
        public OeEfCoreDataAdapter(DbContextOptions options) : this(options, null)
        {
        }
        public OeEfCoreDataAdapter(DbContextOptions options, Db.OeQueryCache queryCache)
            : this(options, queryCache, new OeEfCoreOperationAdapter(typeof(T), _entitySetMetaAdapters))
        {
        }
        public OeEfCoreDataAdapter(DbContextOptions options, Db.OeQueryCache queryCache, OeEfCoreOperationAdapter operationAdapter)
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
            foreach (PropertyInfo property in typeof(T).GetProperties())
            {
                Type dbSetType = property.PropertyType.GetInterface(typeof(IQueryable<>).FullName);
                if (dbSetType != null)
                    entitySetMetaAdapters.Add(CreateDbSetInvoker(property, dbSetType));
            }

            return new Db.OeEntitySetMetaAdapterCollection(entitySetMetaAdapters.ToArray(), new ModelBuilder.OeEdmModelMetadataProvider());
        }
        private static Db.OeEntitySetMetaAdapter CreateDbSetInvoker(PropertyInfo property, Type dbSetType)
        {
            MethodInfo mi = ((Func<PropertyInfo, Db.OeEntitySetMetaAdapter>)CreateDbSetInvoker<Object>).GetMethodInfo().GetGenericMethodDefinition();
            Type entityType = dbSetType.GetGenericArguments()[0];
            MethodInfo func = mi.GetGenericMethodDefinition().MakeGenericMethod(entityType);
            return (Db.OeEntitySetMetaAdapter)func.Invoke(null, new Object[] { property });
        }
        private static Db.OeEntitySetMetaAdapter CreateDbSetInvoker<TEntity>(PropertyInfo property) where TEntity : class
        {
            var getDbSet = (Func<T, DbSet<TEntity>>)property.GetGetMethod().CreateDelegate(typeof(Func<T, DbSet<TEntity>>));
            return new DbSetAdapterImpl<TEntity>(getDbSet, property.Name);
        }
        public override Db.OeAsyncEnumerator ExecuteEnumerator(Object dataContext, OeQueryContext queryContext, CancellationToken cancellationToken)
        {
            IAsyncEnumerable<Object> asyncEnumerable;
            if (base.QueryCache.AllowCache)
                asyncEnumerable = GetFromCache<Object>(queryContext, (T)dataContext, base.QueryCache);
            else
            {
                IQueryable query = CreateQuery(queryContext, dataContext, new OeConstantToVariableVisitor(queryContext.SkipTokenParser != null));
                asyncEnumerable = ((IQueryable<Object>)query).AsAsyncEnumerable();
            }

            Db.OeAsyncEnumerator asyncEnumerator = new OeEfCoreAsyncEnumerator(asyncEnumerable.GetEnumerator(), cancellationToken);
            if (queryContext.CountExpression != null)
            {
                IQueryable query = queryContext.EntitySetAdapter.GetEntitySet(dataContext);
                asyncEnumerator.Count = query.Provider.Execute<int>(queryContext.CountExpression);
            }

            return asyncEnumerator;
        }
        public override TResult ExecuteScalar<TResult>(Object dataContext, OeQueryContext queryContext)
        {
            if (base.QueryCache.AllowCache)
                return GetFromCache<TResult>(queryContext, (T)dataContext, base.QueryCache).Single().GetAwaiter().GetResult();

            IQueryable query = queryContext.EntitySetAdapter.GetEntitySet(dataContext);
            Expression expression = queryContext.CreateExpression(query, new OeConstantToVariableVisitor(false));
            return query.Provider.Execute<TResult>(expression);
        }
        public override Db.OeEntitySetAdapter GetEntitySetAdapter(String entitySetName)
        {
            return new Db.OeEntitySetAdapter(_entitySetMetaAdapters.FindByEntitySetName(entitySetName), this);
        }
        private static IAsyncEnumerable<TResult> GetFromCache<TResult>(OeQueryContext queryContext, T dbContext, Db.OeQueryCache queryCache)
        {
            OeCacheContext cacheContext = queryContext.CreateCacheContext();
            Db.QueryCacheItem queryCacheItem = queryCache.GetQuery(cacheContext);

            Func<QueryContext, IAsyncEnumerable<TResult>> queryExecutor;
            Expression countExpression;
            IReadOnlyList<Db.OeQueryCacheDbParameterValue> parameterValues;
            if (queryCacheItem == null)
            {
                IQueryable query = queryContext.EntitySetAdapter.GetEntitySet(dbContext);
                var parameterVisitor = new OeConstantToParameterVisitor(false);

                Expression expression = queryContext.CreateExpression(query, parameterVisitor);
                queryExecutor = dbContext.CreateAsyncQueryExecutor<TResult>(expression);
                countExpression = queryContext.CreateCountExpression(query, expression);
                queryCache.AddQuery(queryContext.CreateCacheContext(parameterVisitor.ConstantToParameterMapper), queryExecutor, countExpression,
                    queryContext.EntryFactory, queryContext.SkipTokenParser?.Accessors);
                parameterValues = parameterVisitor.ParameterValues;
            }
            else
            {
                queryExecutor = (Func<QueryContext, IAsyncEnumerable<TResult>>)queryCacheItem.Query;
                queryContext.EntryFactory = queryCacheItem.EntryFactory;
                if (queryContext.SkipTokenParser != null)
                    queryContext.SkipTokenParser.Accessors = queryCacheItem.SkipTokenAccessors;
                countExpression = queryCacheItem.CountExpression;
                parameterValues = cacheContext.ParameterValues;
            }

            var queryContextFactory = dbContext.GetService<IQueryContextFactory>();
            QueryContext efQueryContext = queryContextFactory.Create();
            foreach (Db.OeQueryCacheDbParameterValue parameterValue in parameterValues)
                efQueryContext.AddParameter(parameterValue.ParameterName, parameterValue.ParameterValue);

            if (queryContext.ODataUri.QueryCount.GetValueOrDefault())
                queryContext.CountExpression = new OeParameterToVariableVisitor().Translate(countExpression, parameterValues);

            return queryExecutor(efQueryContext);
        }
        private static IAsyncEnumerable<TResult> GetQueryExecutor<TResult>(OeQueryContext queryContext, T dbContext)
        {
            IQueryable query = queryContext.EntitySetAdapter.GetEntitySet(dbContext);

            var parameterVisitor = new OeConstantToParameterVisitor(queryContext.SkipTokenParser != null);
            Expression expression = queryContext.CreateExpression(query, parameterVisitor);
            Func<QueryContext, IAsyncEnumerable<TResult>> queryExecutor = dbContext.CreateAsyncQueryExecutor<TResult>(expression);

            var queryContextFactory = dbContext.GetService<IQueryContextFactory>();
            QueryContext efQueryContext = queryContextFactory.Create();
            foreach (Db.OeQueryCacheDbParameterValue parameterValue in parameterVisitor.ParameterValues)
                efQueryContext.AddParameter(parameterValue.ParameterName, parameterValue.ParameterValue);

            if (queryContext.ODataUri.QueryCount.GetValueOrDefault())
            {
                Expression countExpression = queryContext.CreateCountExpression(query, expression);
                queryContext.CountExpression = new OeParameterToVariableVisitor().Translate(countExpression, parameterVisitor.ParameterValues);
            }

            return queryExecutor(efQueryContext);
        }
        public override Task<int> SaveChangesAsync(IEdmModel edmModel, Object dataContext, CancellationToken cancellationToken)
        {
            var dbContext = (T)dataContext;
            return dbContext.SaveChangesAsync(cancellationToken);
        }

        public sealed override Db.OeEntitySetMetaAdapterCollection EntitySetMetaAdapters => _entitySetMetaAdapters;
    }
}
