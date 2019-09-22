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
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity.EfCore
{
    internal interface IFromSql
    {
        IQueryable FromSql(Object dataContext, String sql, Object[] parameters);
    }

    public class OeEfCoreDataAdapter<T> : Db.OeDataAdapter, IDisposable where T : DbContext
    {
        private sealed class DbQueryAdapterImpl<TEntity> : Db.OeEntitySetAdapter, IFromSql where TEntity : class
        {
            public DbQueryAdapterImpl(String entitySetName)
            {
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
            public IQueryable FromSql(Object dataContext, String sql, Object[] parameters)
            {
                var queryable = (DbSet<TEntity>)GetEntitySet(dataContext);
                return queryable.FromSqlRaw(sql, parameters);
            }
            public override IQueryable GetEntitySet(Object dataContext)
            {
                return new EntityQueryable<TEntity>(((T)dataContext).GetDependencies().QueryProvider);
            }
            public override void RemoveEntity(Object dataContext, ODataResourceBase entry)
            {
                throw new NotSupportedException();
            }

            public override Type EntityType => typeof(TEntity);
            public override String EntitySetName { get; }
            public override bool IsDbQuery => true;

        }
        private sealed class DbSetAdapterImpl<TEntity> : Db.OeEntitySetAdapter, IFromSql where TEntity : class
        {
            private IEntityType _entityType;
            private Func<MaterializationContext, Object> _materializer;
            private Func<Object[]> _valueBufferArrayInit;
            private IForeignKey _selfReferenceKey;

            public DbSetAdapterImpl(String entitySetName)
            {
                EntitySetName = entitySetName;
            }

            public override void AddEntity(Object dataContext, ODataResourceBase entry)
            {
                var context = (DbContext)dataContext;
                EntityEntry<TEntity> entityEntry = context.Add(CreateEntity(context, entry));
                InternalEntityEntry internalEntityEntry = entityEntry.GetInfrastructure();

                IReadOnlyList<IProperty> keyProperties = _entityType.FindPrimaryKey().Properties;
                for (int i = 0; i < keyProperties.Count; i++)
                    if (keyProperties[i].ValueGenerated == ValueGenerated.OnAdd)
                    {
                        Object value = internalEntityEntry.GetCurrentValue(keyProperties[i]);
                        internalEntityEntry.SetTemporaryValue(keyProperties[i], value, false);
                    }
            }
            public override void AttachEntity(Object dataContext, ODataResourceBase entry)
            {
                var context = (T)dataContext;
                InternalEntityEntry internalEntry = GetEntityEntry(context, entry);
                if (internalEntry == null)
                {
                    TEntity entity = CreateEntity(context, entry);
                    internalEntry = context.Attach(entity).GetInfrastructure();

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
                        internalEntry.SetProperty(property, value, false);
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
                var dbSet = new InternalDbSet<TEntity>((T)dataContext);
                return dbSet.FromSqlRaw(sql, parameters);
            }
            public override IQueryable GetEntitySet(Object dataContext)
            {
                return new EntityQueryable<TEntity>(((T)dataContext).GetDependencies().QueryProvider);
            }
            private InternalEntityEntry GetEntityEntry(T context, ODataResourceBase entity)
            {
                Initialize(context);
                IStateManager stateManager = ((IDbContextDependencies)context).StateManager;
                return stateManager.TryGetEntry(_entityType.FindPrimaryKey(), GetKeyValues(entity));
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
                    if (_selfReferenceKey == null)
                        context.Attach(entity);
                    else
                    {
                        var finder = (IEntityFinder<TEntity>)context.GetDependencies().EntityFinderFactory.Create(_entityType);
                        entity = finder.Find(GetKeyValues(entry));
                    }
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
        private static Db.OeEntitySetAdapterCollection _entitySetAdapters;

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
            : this(options, queryCache, new OeEfCoreOperationAdapter(typeof(T)))
        {
        }
        public OeEfCoreDataAdapter(DbContextOptions options, Cache.OeQueryCache queryCache, OeEfCoreOperationAdapter operationAdapter)
            : base(queryCache, operationAdapter)
        {
            if (options != null)
                _dbContextPool = new DbContextPool<T>(Fix.FixHelper.FixDistinctCount(options));
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
        private static Db.OeEntitySetAdapterCollection CreateEntitySetAdapters(IModel efModel)
        {
            var entitySetAdapters = new List<Db.OeEntitySetAdapter>();
            foreach (PropertyInfo property in typeof(T).GetProperties())
            {
                if (typeof(IQueryable).IsAssignableFrom(property.PropertyType))
                {
                    Type entityType = property.PropertyType.GetGenericArguments()[0];
                    bool isDbQuery = efModel.FindEntityType(entityType).FindPrimaryKey() == null;
                    entitySetAdapters.Add(CreateEntitySetAdapter(entityType, property.Name, isDbQuery));
                }
            }

            return new Db.OeEntitySetAdapterCollection(entitySetAdapters.ToArray());
        }
        protected static Db.OeEntitySetAdapter CreateEntitySetAdapter(Type entityType, String entitySetName, bool isDbQuery)
        {
            MethodInfo mi = ((Func<String, bool, Db.OeEntitySetAdapter>)CreateEntitySetAdapter<Object>).GetMethodInfo().GetGenericMethodDefinition();
            MethodInfo func = mi.GetGenericMethodDefinition().MakeGenericMethod(entityType);
            return (Db.OeEntitySetAdapter)func.Invoke(null, new Object[] { entitySetName, isDbQuery });
        }
        private static Db.OeEntitySetAdapter CreateEntitySetAdapter<TEntity>(String entitySetName, bool isDbQuery) where TEntity : class
        {
            return isDbQuery ? (Db.OeEntitySetAdapter)new DbQueryAdapterImpl<TEntity>(entitySetName) : new DbSetAdapterImpl<TEntity>(entitySetName);
        }
        public void Dispose()
        {
            if (_dbContextPool != null)
                _dbContextPool.Dispose();
        }
        public override IAsyncEnumerable<Object> Execute(Object dataContext, OeQueryContext queryContext)
        {
            IAsyncEnumerable<Object> asyncEnumerable;
            MethodCallExpression countExpression = null;
            if (base.QueryCache.AllowCache)
                asyncEnumerable = GetFromCache<Object>(queryContext, (T)dataContext, out countExpression);
            else
            {
                Expression expression = queryContext.CreateExpression(new OeConstantToVariableVisitor());
                expression = OeEnumerableToQuerableVisitor.Translate(expression);
                expression = TranslateExpression(expression);
                IQueryable entitySet = queryContext.EntitySetAdapter.GetEntitySet(dataContext);
                IQueryable query = entitySet.Provider.CreateQuery(queryContext.TranslateSource(dataContext, expression));
                asyncEnumerable = ((IQueryable<Object>)query).AsAsyncEnumerable();

                if (queryContext.IsQueryCount())
                {
                    expression = queryContext.CreateCountExpression(query.Expression);
                    countExpression = (MethodCallExpression)OeEnumerableToQuerableVisitor.Translate(expression);
                    countExpression = (MethodCallExpression)TranslateExpression(countExpression);
                }
            }

            if (countExpression != null)
            {
                IQueryable query = queryContext.EntitySetAdapter.GetEntitySet(dataContext);
                queryContext.TotalCountOfItems = query.Provider.Execute<int>(countExpression);
            }

            return asyncEnumerable;
        }
        public override TResult ExecuteScalar<TResult>(Object dataContext, OeQueryContext queryContext)
        {
            if (base.QueryCache.AllowCache)
                return GetFromCache<TResult>(queryContext, (T)dataContext, out _).SingleAsync().GetAwaiter().GetResult();

            IQueryable query = queryContext.EntitySetAdapter.GetEntitySet(dataContext);
            Expression expression = queryContext.CreateExpression(new OeConstantToVariableVisitor());
            expression = OeEnumerableToQuerableVisitor.Translate(expression);
            expression = TranslateExpression(expression);
            return query.Provider.Execute<TResult>(queryContext.TranslateSource(dataContext, expression));
        }
        private static Db.OeEntitySetAdapterCollection GetEntitySetAdapters(Db.OeDataAdapter dataAdapter)
        {
            Db.OeEntitySetAdapterCollection entitySetAdapters = Volatile.Read(ref _entitySetAdapters);
            if (entitySetAdapters != null)
                return entitySetAdapters;

            var context = (DbContext)dataAdapter.CreateDataContext();
            try
            {
                Interlocked.CompareExchange(ref _entitySetAdapters, CreateEntitySetAdapters(context.Model), null);
                return Volatile.Read(ref _entitySetAdapters);
            }
            finally
            {
                dataAdapter.CloseDataContext(context);
            }
        }
        private IAsyncEnumerable<TResult> GetFromCache<TResult>(OeQueryContext queryContext, T dbContext, out MethodCallExpression countExpression)
        {
            Cache.OeCacheContext cacheContext = queryContext.CreateCacheContext();
            Cache.OeQueryCacheItem queryCacheItem = base.QueryCache.GetQuery(cacheContext);

            Func<QueryContext, IAsyncEnumerable<TResult>> queryExecutor;
            IReadOnlyList<Cache.OeQueryCacheDbParameterValue> parameterValues;
            if (queryCacheItem == null)
            {
                var parameterVisitor = new OeConstantToParameterVisitor();
                Expression expression = queryContext.CreateExpression(parameterVisitor);
                expression = queryContext.TranslateSource(dbContext, expression);
                expression = OeEnumerableToQuerableVisitor.Translate(expression);
                expression = TranslateExpression(expression);

                queryExecutor = dbContext.CreateAsyncQueryExecutor<TResult>(expression);
                countExpression = queryContext.CreateCountExpression(expression);
                countExpression = (MethodCallExpression)OeEnumerableToQuerableVisitor.Translate(countExpression);
                countExpression = (MethodCallExpression)TranslateExpression(countExpression);

                base.QueryCache.AddQuery(queryContext.CreateCacheContext(parameterVisitor.ConstantToParameterMapper), queryExecutor, countExpression, queryContext.EntryFactory);
                parameterValues = parameterVisitor.ParameterValues;
            }
            else
            {
                queryExecutor = (Func<QueryContext, IAsyncEnumerable<TResult>>)queryCacheItem.Query;
                queryContext.EntryFactory = queryCacheItem.EntryFactory;
                countExpression = queryCacheItem.CountExpression;
                parameterValues = cacheContext.ParameterValues;
            }

            var queryContextFactory = dbContext.GetService<IQueryContextFactory>();
            QueryContext efQueryContext = queryContextFactory.Create();
            foreach (Cache.OeQueryCacheDbParameterValue parameterValue in parameterValues)
                efQueryContext.AddParameter(parameterValue.ParameterName, parameterValue.ParameterValue);

            if (queryContext.IsQueryCount())
            {
                countExpression = (MethodCallExpression)queryContext.TranslateSource(dbContext, countExpression);
                countExpression = (MethodCallExpression)new OeParameterToVariableVisitor().Translate(countExpression, parameterValues);
            }
            else
                countExpression = null;

            return queryExecutor(efQueryContext);
        }
        public override Task<int> SaveChangesAsync(Object dataContext, CancellationToken cancellationToken)
        {
            var dbContext = (T)dataContext;
            return dbContext.SaveChangesAsync(cancellationToken);
        }
        protected virtual Expression TranslateExpression(Expression expression)
        {
            return new Fix.FixSelectDistinctVisitor().Visit(expression);
        }

        public override Type DataContextType => typeof(T);
        public override Db.OeEntitySetAdapterCollection EntitySetAdapters => GetEntitySetAdapters(this);
    }
}