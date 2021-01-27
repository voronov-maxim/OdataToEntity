using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
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
        IQueryable FromSql(Object dataContext, String sql, Object?[] parameters);
    }

    public class OeEfCoreDataAdapter<T> : Db.OeDataAdapter, IDisposable where T : notnull, DbContext
    {
        private readonly DbContextPool<T>? _dbContextPool;
        private static Db.OeEntitySetAdapterCollection? _entitySetAdapters;

        public OeEfCoreDataAdapter() : this(null, null)
        {
        }
        public OeEfCoreDataAdapter(DbContextOptions<T> options) : this(options, null)
        {
        }
        public OeEfCoreDataAdapter(Cache.OeQueryCache queryCache) : this(null, queryCache)
        {
        }
        public OeEfCoreDataAdapter(DbContextOptions<T>? options, Cache.OeQueryCache? queryCache)
            : this(options, queryCache, new OeEfCoreOperationAdapter(typeof(T)))
        {
        }
        public OeEfCoreDataAdapter(DbContextOptions<T>? options, Cache.OeQueryCache? queryCache, OeEfCoreOperationAdapter operationAdapter)
            : base(queryCache, operationAdapter)
        {
            if (options != null)
                _dbContextPool = new DbContextPool<T>(options);
        }

        public override void CloseDataContext(Object dataContext)
        {
            var dbContext = (DbContext)dataContext;
            dbContext.Dispose();
        }
        public override Object CreateDataContext()
        {
            DbContext dbContext;
            if (_dbContextPool == null)
                dbContext = Infrastructure.FastActivator.CreateInstance<T>();
            else
                dbContext = (DbContext)new DbContextLease(_dbContextPool, true).Context;

            dbContext.ChangeTracker.AutoDetectChangesEnabled = false;
            dbContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
            return dbContext;
        }
        private static Db.OeEntitySetAdapterCollection CreateEntitySetAdapters(IModel efModel)
        {
            var entitySetAdapters = new List<Db.OeEntitySetAdapter>();
            foreach (PropertyInfo property in typeof(T).GetProperties())
                if (typeof(IQueryable).IsAssignableFrom(property.PropertyType))
                {
                    Type entityType = property.PropertyType.GetGenericArguments()[0];
                    bool isDbQuery = efModel.FindEntityType(entityType).FindPrimaryKey() == null;
                    Db.OeEntitySetAdapter entitySetAdapter = CreateEntitySetAdapter(entityType, property.Name, isDbQuery);
                    entitySetAdapters.Add(entitySetAdapter);
                }

            return new Db.OeEntitySetAdapterCollection(entitySetAdapters.ToArray());
        }
        protected static Db.OeEntitySetAdapter CreateEntitySetAdapter(Type entityType, String entitySetName, bool isDbQuery)
        {
            return isDbQuery ? (Db.OeEntitySetAdapter)new OeDbQueryAdapter(entityType, entitySetName) : new OeDbSetAdapter(entityType, entitySetName);
        }
        public void Dispose()
        {
            if (_dbContextPool != null)
                _dbContextPool.Dispose();
        }
        public override IAsyncEnumerable<Object> Execute(Object dataContext, OeQueryContext queryContext)
        {
            IAsyncEnumerable<Object> asyncEnumerable;
            MethodCallExpression? countExpression = null;
            if (base.QueryCache.AllowCache)
                asyncEnumerable = GetFromCache<Object>(queryContext, (T)dataContext, out countExpression);
            else
            {
                Expression expression = queryContext.CreateExpression(new OeConstantToVariableVisitor());
                expression = OeEnumerableToQuerableVisitor.Translate(expression);
                expression = TranslateExpression(queryContext.EdmModel, expression);
                IQueryable entitySet = queryContext.EntitySetAdapter.GetEntitySet(dataContext);
                IQueryable query = entitySet.Provider.CreateQuery(queryContext.TranslateSource(dataContext, expression));
                asyncEnumerable = ((IQueryable<Object>)query).AsAsyncEnumerable();

                if (queryContext.IsQueryCount())
                {
                    expression = queryContext.CreateCountExpression(query.Expression);
                    countExpression = (MethodCallExpression)OeEnumerableToQuerableVisitor.Translate(expression);
                    countExpression = (MethodCallExpression)TranslateExpression(queryContext.EdmModel, countExpression);
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
                return GetFromCache<TResult>(queryContext, (DbContext)dataContext, out _).SingleAsync().GetAwaiter().GetResult();

            IQueryable query = queryContext.EntitySetAdapter.GetEntitySet(dataContext);
            Expression expression = queryContext.CreateExpression(new OeConstantToVariableVisitor());
            expression = OeEnumerableToQuerableVisitor.Translate(expression);
            expression = TranslateExpression(queryContext.EdmModel, expression);
            return query.Provider.Execute<TResult>(queryContext.TranslateSource(dataContext, expression));
        }
        private static Db.OeEntitySetAdapterCollection GetEntitySetAdapters(Db.OeDataAdapter dataAdapter)
        {
            Db.OeEntitySetAdapterCollection? entitySetAdapters = Volatile.Read(ref _entitySetAdapters);
            if (entitySetAdapters != null)
                return entitySetAdapters;

            var context = (DbContext)dataAdapter.CreateDataContext();
            try
            {
                Interlocked.CompareExchange(ref _entitySetAdapters, CreateEntitySetAdapters(context.Model), null);
                return Volatile.Read(ref _entitySetAdapters)!;
            }
            finally
            {
                dataAdapter.CloseDataContext(context);
            }
        }
        private IAsyncEnumerable<TResult> GetFromCache<TResult>(OeQueryContext queryContext, DbContext dbContext, out MethodCallExpression? countExpression)
        {
            Cache.OeCacheContext cacheContext = queryContext.CreateCacheContext();
            Cache.OeQueryCacheItem? queryCacheItem = base.QueryCache.GetQuery(cacheContext);

            Func<QueryContext, IAsyncEnumerable<TResult>> queryExecutor;
            IReadOnlyList<Cache.OeQueryCacheDbParameterValue> parameterValues;
            if (queryCacheItem == null)
            {
                var parameterVisitor = new OeConstantToParameterVisitor();
                Expression expression = queryContext.CreateExpression(parameterVisitor);
                expression = TranslateExpression(queryContext.EdmModel, expression);
                expression = queryContext.TranslateSource(dbContext, expression);
                expression = OeEnumerableToQuerableVisitor.Translate(expression);

                queryExecutor = dbContext.CreateAsyncQueryExecutor<TResult>(expression);
                if (queryContext.EntryFactory == null)
                    countExpression = null;
                else
                {
                    countExpression = queryContext.CreateCountExpression(expression);
                    countExpression = (MethodCallExpression)OeEnumerableToQuerableVisitor.Translate(countExpression);
                    countExpression = (MethodCallExpression)TranslateExpression(queryContext.EdmModel, countExpression);
                }

                cacheContext = queryContext.CreateCacheContext(parameterVisitor.ConstantToParameterMapper);
                base.QueryCache.AddQuery(cacheContext, queryExecutor, countExpression, queryContext.EntryFactory);
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

            if (queryContext.IsQueryCount() && countExpression != null)
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
        protected virtual Expression TranslateExpression(IEdmModel edmModel, Expression expression)
        {
            return expression;
        }

        public override Type DataContextType => typeof(T);
        public override Db.OeEntitySetAdapterCollection EntitySetAdapters => GetEntitySetAdapters(this);
    }
}