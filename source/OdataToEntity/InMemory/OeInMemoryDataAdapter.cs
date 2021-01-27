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
    public class OeInMemoryDataAdapter<T> : Db.OeDataAdapter where T : notnull
    {
        private static Db.OeEntitySetAdapterCollection _entitySetAdapters = CreateEntitySetAdapters();
        private readonly T _dataContext;

        public OeInMemoryDataAdapter(T dataContext) : this(dataContext, null)
        {
        }
        public OeInMemoryDataAdapter(T dataContext, Cache.OeQueryCache? queryCache)
            : base(queryCache, OeInMemoryOperationAdapter.Instance)
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
            if (base.QueryCache.AllowCache && false)
            {
                enumerable = GetFromCache<Object>(queryContext, out countExpression);
            }
            else
            {
                Expression expression = queryContext.CreateExpression(out IReadOnlyDictionary<ConstantExpression, ConstantNode> constants);
                expression = new OeSingleNavigationVisitor(queryContext.EdmModel).Visit(expression);
                expression = new OeCollectionNavigationVisitor(queryContext.EdmModel).Visit(expression);
                expression = new NullPropagationVisitor().Visit(expression);
                expression = new OeConstantToVariableVisitor().Translate(expression, constants);
                expression = queryContext.TranslateSource(dataContext, expression);
                var func = (Func<IEnumerable>)Expression.Lambda(expression).Compile();
                enumerable = func();

                if (queryContext.IsQueryCount())
                    countExpression = queryContext.CreateCountExpression(expression);
            }

            if (countExpression != null)
            {
                countExpression = (MethodCallExpression)new NullPropagationVisitor().Visit(countExpression);
                var func = (Func<int>)Expression.Lambda(countExpression).Compile();
                queryContext.TotalCountOfItems = func();
            }

            return Infrastructure.AsyncEnumeratorHelper.ToAsyncEnumerable(enumerable);
        }
        public override TResult ExecuteScalar<TResult>(Object dataContext, OeQueryContext queryContext)
        {
            Expression expression = queryContext.CreateExpression(out IReadOnlyDictionary<ConstantExpression, ConstantNode> constants);
            expression = new OeSingleNavigationVisitor(queryContext.EdmModel).Visit(expression);
            expression = new OeCollectionNavigationVisitor(queryContext.EdmModel).Visit(expression);
            expression = new NullPropagationVisitor().Visit(expression);
            expression = new OeConstantToVariableVisitor().Translate(expression, constants);
            expression = queryContext.TranslateSource(dataContext, expression);
            var func = (Func<TResult>)Expression.Lambda(expression).Compile();
            return func();
        }
        private IEnumerable<TResult> GetFromCache<TResult>(OeQueryContext queryContext, out MethodCallExpression? countExpression)
        {
            Cache.OeCacheContext cacheContext = queryContext.CreateCacheContext();
            Cache.OeQueryCacheItem? queryCacheItem = base.QueryCache.GetQuery(cacheContext);

            IReadOnlyList<Cache.OeQueryCacheDbParameterValue> parameterValues;
            if (queryCacheItem == null || true)
            {
                var variableVisitor = new OeConstantToVariableVisitor();
                Expression expression = queryContext.CreateExpression(variableVisitor);
                expression = queryContext.TranslateSource(_dataContext, expression);

                if (queryContext.EntryFactory == null)
                    countExpression = null;
                else
                {
                    countExpression = queryContext.CreateCountExpression(expression);
                }

                //cacheContext = queryContext.CreateCacheContext(variableVisitor.ConstantToParameterMapper);
                //base.QueryCache.AddQuery(cacheContext, expression, countExpression, queryContext.EntryFactory);
                //parameterValues = variableVisitor.ParameterValues;
            }
            else
            {
                queryContext.EntryFactory = queryCacheItem.EntryFactory;
                countExpression = queryCacheItem.CountExpression;
                parameterValues = cacheContext.ParameterValues;
            }

            //foreach (Cache.OeQueryCacheDbParameterValue parameterValue in parameterValues)
            //    efQueryContext.AddParameter(parameterValue.ParameterName, parameterValue.ParameterValue);

            if (queryContext.IsQueryCount() && countExpression != null)
            {
                countExpression = (MethodCallExpression)queryContext.TranslateSource(_dataContext, countExpression);
                //countExpression = (MethodCallExpression)new OeParameterToVariableVisitor().Translate(countExpression, parameterValues);
            }
            else
                countExpression = null;

            return null!;// queryExecutor(efQueryContext);
        }
        public override Task<int> SaveChangesAsync(Object dataContext, CancellationToken cancellationToken)
        {
            return Task.FromResult(-1);
        }

        public override Type DataContextType => typeof(T);
        public override Db.OeEntitySetAdapterCollection EntitySetAdapters => _entitySetAdapters;
    }
}
