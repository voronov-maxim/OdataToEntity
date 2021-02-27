using Microsoft.OData.UriParser;
using OdataToEntity.Parsers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity.InMemory
{
    public class InMemoryDataAdapter<T> : Db.OeDataAdapter where T : notnull
    {
        private static readonly Db.OeEntitySetAdapterCollection _entitySetAdapters = CreateEntitySetAdapters();
        private readonly T _dataContext;

        public InMemoryDataAdapter(T dataContext) : this(dataContext, null)
        {
        }
        public InMemoryDataAdapter(T dataContext, Cache.OeQueryCache? queryCache)
            : base(queryCache, InMemoryOperationAdapter.Instance)
        {
            _dataContext = dataContext;
        }

        public override void CloseDataContext(Object dataContext)
        {
        }
        public override Object CreateDataContext()
        {
            return _dataContext;
        }
        private static Db.OeEntitySetAdapterCollection CreateEntitySetAdapters()
        {
            var entitySetAdapters = new List<Db.OeEntitySetAdapter>();
            foreach (PropertyInfo property in typeof(T).GetProperties())
                if (property.PropertyType.IsGenericType && typeof(IEnumerable).IsAssignableFrom(property.PropertyType))
                {
                    Type entityType = property.PropertyType.GetGenericArguments()[0];
                    IReadOnlyList<PropertyInfo> keys = ModelBuilder.OeModelBuilderHelper.GetKeyProperties(entityType);
                    entitySetAdapters.Add(new InMemoryEntitySetAdapter(entityType, property.Name, keys));
                }

            return new Db.OeEntitySetAdapterCollection(entitySetAdapters.ToArray());
        }
        public override IAsyncEnumerable<Object> Execute(Object dataContext, OeQueryContext queryContext)
        {
            IEnumerable enumerable;
            MethodCallExpression? countExpression = null;
            if (base.QueryCache.AllowCache)
                enumerable = GetFromCache<Object>(queryContext, dataContext, out countExpression);
            else
            {
                var constantToVariableVisitor = new InMemoryConstantToVariableVisitor();
                Expression expression = queryContext.CreateExpression(out IReadOnlyDictionary<ConstantExpression, ConstantNode> constants);
                expression = new OeSingleNavigationVisitor(queryContext.EdmModel).Visit(expression);
                expression = new OeCollectionNavigationVisitor(queryContext.EdmModel).Visit(expression);
                expression = new NullPropagationVisitor().Visit(expression);
                expression = queryContext.TranslateSource(dataContext, expression);
                var func = (Func<IEnumerable>)Expression.Lambda(expression).Compile();
                enumerable = func();

                if (queryContext.IsQueryCount())
                    countExpression = queryContext.CreateCountExpression(expression);
            }

            if (countExpression != null)
            {
                var func = (Func<int>)Expression.Lambda(countExpression).Compile();
                queryContext.TotalCountOfItems = func();
            }

            return Infrastructure.AsyncEnumeratorHelper.ToAsyncEnumerable(enumerable);
        }
        public override TResult ExecuteScalar<TResult>(Object dataContext, OeQueryContext queryContext)
        {
            Cache.OeCacheContext cacheContext = queryContext.CreateCacheContext();
            Cache.OeQueryCacheItem? queryCacheItem = base.QueryCache.GetQuery(cacheContext);

            InMemoryScalarExecutor<TResult> executor;
            IReadOnlyList<Cache.OeQueryCacheDbParameterValue> parameterValues;
            if (queryCacheItem == null)
            {
                var variableVisitor = new InMemoryConstantToVariableVisitor();
                Expression expression = queryContext.CreateExpression(out IReadOnlyDictionary<ConstantExpression, ConstantNode> constants);
                expression = variableVisitor.Translate(expression, constants);
                expression = new OeSingleNavigationVisitor(queryContext.EdmModel).Visit(expression);
                expression = new OeCollectionNavigationVisitor(queryContext.EdmModel).Visit(expression);
                expression = new NullPropagationVisitor().Visit(expression);
                expression = queryContext.TranslateSource(dataContext, expression);
                var query = (Func<TResult>)Expression.Lambda(expression).Compile();

                cacheContext = queryContext.CreateCacheContext(variableVisitor.ConstantToParameterMapper);
                executor = new InMemoryScalarExecutor<TResult>(query, variableVisitor.ParameterValues, variableVisitor.Parameters);
                base.QueryCache.AddQuery(cacheContext, (query, variableVisitor.Parameters), null, queryContext.EntryFactory);
                parameterValues = variableVisitor.ParameterValues;
            }
            else
            {
                executor = (InMemoryScalarExecutor<TResult>)queryCacheItem.Query;
                queryContext.EntryFactory = queryCacheItem.EntryFactory;
                parameterValues = cacheContext.ParameterValues;
            }

            lock (executor)
            {
                for (int i = 0; i < parameterValues.Count; i++)
                    executor[parameterValues[i].ParameterName] = parameterValues[i].ParameterValue;
                return executor.Execute();
            }
        }
        private IEnumerable GetFromCache<TResult>(OeQueryContext queryContext, Object dataContext, out MethodCallExpression? countExpression)
        {
            Cache.OeCacheContext cacheContext = queryContext.CreateCacheContext();
            Cache.OeQueryCacheItem? queryCacheItem = base.QueryCache.GetQuery(cacheContext);

            InMemoryExecutor executor;
            IReadOnlyList<Cache.OeQueryCacheDbParameterValue> parameterValues;
            if (queryCacheItem == null)
            {
                var variableVisitor = new InMemoryConstantToVariableVisitor();
                Expression expression = queryContext.CreateExpression(out IReadOnlyDictionary<ConstantExpression, ConstantNode> constants);
                expression = variableVisitor.Translate(expression, constants);
                expression = new OeSingleNavigationVisitor(queryContext.EdmModel).Visit(expression);
                expression = new OeCollectionNavigationVisitor(queryContext.EdmModel).Visit(expression);
                expression = new NullPropagationVisitor().Visit(expression);
                expression = new InMemorySourceVisitor(queryContext.EdmModel, variableVisitor.Parameters).Visit(expression);
                var query = (Func<IEnumerable>)Expression.Lambda(expression).Compile();

                if (queryContext.EntryFactory == null)
                    countExpression = null;
                else
                    countExpression = queryContext.CreateCountExpression(expression);

                cacheContext = queryContext.CreateCacheContext(variableVisitor.ConstantToParameterMapper);
                executor = new InMemoryExecutor(query, variableVisitor.ParameterValues, variableVisitor.Parameters);
                base.QueryCache.AddQuery(cacheContext, executor, countExpression, queryContext.EntryFactory);
                parameterValues = variableVisitor.ParameterValues;
            }
            else
            {
                executor = (InMemoryExecutor)queryCacheItem.Query;
                queryContext.EntryFactory = queryCacheItem.EntryFactory;
                countExpression = queryCacheItem.CountExpression;
                parameterValues = cacheContext.ParameterValues;
            }

            if (!queryContext.IsQueryCount())
                countExpression = null;

            executor.Wait();
            executor.SetDataContext(dataContext);
            for (int i = 0; i < parameterValues.Count; i++)
                executor[parameterValues[i].ParameterName] = parameterValues[i].ParameterValue;
            return executor;
        }
        public override Task<int> SaveChangesAsync(Object dataContext, CancellationToken cancellationToken)
        {
            return Task.FromResult(-1);
        }

        public override Type DataContextType => typeof(T);
        public override Db.OeEntitySetAdapterCollection EntitySetAdapters => _entitySetAdapters;
    }
}
