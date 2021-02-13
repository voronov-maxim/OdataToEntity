using LinqToDB;
using LinqToDB.Data;
using Microsoft.OData.Edm;
using OdataToEntity.Parsers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity.Linq2Db
{
    public class OeLinq2DbDataAdapter<T> : Db.OeDataAdapter where T : DataConnection, IOeLinq2DbDataContext
    {
        private sealed class ParameterVisitor : ExpressionVisitor
        {
            private readonly Dictionary<ParameterExpression, ParameterExpression> _parameters;

            public ParameterVisitor()
            {
                _parameters = new Dictionary<ParameterExpression, ParameterExpression>();
            }

            protected override Expression VisitNew(NewExpression node)
            {
                var arguments = new Expression[node.Arguments.Count];
                for (int i = 0; i < arguments.Length; i++)
                {
                    Expression argument = base.Visit(node.Arguments[i]);
                    if (argument is MethodCallExpression call && call.Type.GetTypeInfo().IsGenericType && call.Type.GetGenericTypeDefinition() == typeof(IOrderedEnumerable<>))
                    {
                        Type type = call.Type.GetGenericArguments()[0];
                        MethodInfo selectMethodInfo = OeMethodInfoHelper.GetSelectMethodInfo(type, type);
                        ParameterExpression parameter = Expression.Parameter(type);
                        argument = Expression.Call(selectMethodInfo, call, Expression.Lambda(parameter, parameter));
                    }
                    arguments[i] = argument;
                }
                return OeExpressionHelper.CreateTupleExpression(arguments);
            }
            protected override Expression VisitParameter(ParameterExpression node)
            {
                if (_parameters.TryGetValue(node, out ParameterExpression? parameter))
                    return parameter;

                parameter = Expression.Parameter(node.Type, node.Name ?? node.ToString());
                _parameters.Add(node, parameter);
                return parameter;
            }
        }

        private IEdmModel? _edmModel;
        private readonly static Db.OeEntitySetAdapterCollection _entitySetAdapters = CreateEntitySetAdapters();

        public OeLinq2DbDataAdapter() : this(null)
        {
        }
        public OeLinq2DbDataAdapter(Cache.OeQueryCache? queryCache)
            : base(queryCache, new OeLinq2DbOperationAdapter(typeof(T)))
        {
        }
        public OeLinq2DbDataAdapter(Cache.OeQueryCache? queryCache, Db.OeOperationAdapter operationAdapter)
            : base(queryCache, operationAdapter)
        {

        }

        public override void CloseDataContext(Object dataContext)
        {
            var dbContext = (T)dataContext;
            dbContext.Dispose();
        }
        public override Object CreateDataContext()
        {
            if (_edmModel == null)
                throw new InvalidOperationException("Invoke " + nameof(SetEdmModel));

            var dataContext = Infrastructure.FastActivator.CreateInstance<T>();
            dataContext.DataContext = new OeLinq2DbDataContext(GetEdmModel(_edmModel), _entitySetAdapters);
            return dataContext;

            static IEdmModel GetEdmModel(IEdmModel edmModel)
            {
                Db.OeDataAdapter dataAdapter = edmModel.GetAnnotationValue<Db.OeDataAdapter>(edmModel.EntityContainer);
                if (dataAdapter.DataContextType == typeof(T))
                    return edmModel;

                foreach (IEdmModel refModel in edmModel.ReferencedModels)
                    if (refModel.EntityContainer != null && refModel is EdmModel)
                        return GetEdmModel(edmModel);

                throw new InvalidOperationException("EdmModel not found for data context type " + typeof(T).FullName);
            }
        }
        private static Db.OeEntitySetAdapterCollection CreateEntitySetAdapters()
        {
            var entitySetAdapters = new List<Db.OeEntitySetAdapter>();
            foreach (PropertyInfo property in typeof(T).GetTypeInfo().GetProperties())
            {
                Type? entitySetType = property.PropertyType.GetTypeInfo().GetInterface(typeof(IQueryable<>).FullName!);
                if (entitySetType != null)
                    entitySetAdapters.Add(CreateEntitySetAdapter(property, entitySetType));
            }
            return new Db.OeEntitySetAdapterCollection(entitySetAdapters.ToArray());
        }
        private static Db.OeEntitySetAdapter CreateEntitySetAdapter(PropertyInfo property, Type entitySetType)
        {
            MethodInfo mi = ((Func<PropertyInfo, Db.OeEntitySetAdapter>)CreateEntitySetInvoker<Object>).GetMethodInfo().GetGenericMethodDefinition();
            Type entityType = entitySetType.GetTypeInfo().GetGenericArguments()[0];
            MethodInfo func = mi.GetGenericMethodDefinition().MakeGenericMethod(entityType);
            return (Db.OeEntitySetAdapter)func.Invoke(null, new Object[] { property })!;
        }
        private static Db.OeEntitySetAdapter CreateEntitySetInvoker<TEntity>(PropertyInfo property) where TEntity : class
        {
            var getEntitySet = (Func<T, ITable<TEntity>>)property.GetGetMethod()!.CreateDelegate(typeof(Func<T, ITable<TEntity>>));
            var tableAttribute = typeof(TEntity).GetCustomAttribute<LinqToDB.Mapping.TableAttribute>();
            if (tableAttribute == null)
                throw new InvalidOperationException(typeof(TEntity).Name + " missing TableAttribute");

            return new OeLinq2DbSetAdapter<T, TEntity>(getEntitySet, property.Name, tableAttribute.IsView);
        }
        public override IAsyncEnumerable<Object> Execute(Object dataContext, OeQueryContext queryContext)
        {
            Expression expression;
            MethodCallExpression? countExpression = null;
            IQueryable entitySet = queryContext.EntitySetAdapter.GetEntitySet(dataContext);
            if (base.QueryCache.AllowCache)
                expression = GetFromCache(queryContext, (T)dataContext, base.QueryCache, out countExpression);
            else
            {
                expression = queryContext.CreateExpression(new OeConstantToVariableVisitor());
                expression = queryContext.TranslateSource(dataContext, expression);
                expression = new ParameterVisitor().Visit(expression);

                if (queryContext.IsQueryCount())
                    countExpression = queryContext.CreateCountExpression(expression);
            }

            IQueryable<Object> query = (IQueryable<Object>)entitySet.Provider.CreateQuery(expression);
            IAsyncEnumerable<Object> asyncEnumerator = Infrastructure.AsyncEnumeratorHelper.ToAsyncEnumerable(query);

            if (countExpression != null)
                queryContext.TotalCountOfItems = entitySet.Provider.Execute<int>(countExpression);

            return asyncEnumerator;
        }
        public override TResult ExecuteScalar<TResult>(Object dataContext, OeQueryContext queryContext)
        {
            IQueryable query = queryContext.EntitySetAdapter.GetEntitySet(dataContext);
            Expression expression;
            if (base.QueryCache.AllowCache)
                expression = GetFromCache(queryContext, (T)dataContext, base.QueryCache, out _);
            else
            {
                expression = queryContext.CreateExpression(new OeConstantToVariableVisitor());
                expression = queryContext.TranslateSource(dataContext, expression);
                expression = new ParameterVisitor().Visit(expression);
            }
            return query.Provider.Execute<TResult>(expression);
        }
        private static Expression GetFromCache(OeQueryContext queryContext, T dbContext, Cache.OeQueryCache queryCache,
            out MethodCallExpression? countExpression)
        {
            Cache.OeCacheContext cacheContext = queryContext.CreateCacheContext();
            Cache.OeQueryCacheItem? queryCacheItem = queryCache.GetQuery(cacheContext);

            Expression expression;
            IReadOnlyList<Cache.OeQueryCacheDbParameterValue> parameterValues;
            if (queryCacheItem == null)
            {
                var parameterVisitor = new OeConstantToParameterVisitor();
                expression = queryContext.CreateExpression(parameterVisitor);
                expression = new ParameterVisitor().Visit(expression);

                if (queryContext.EntryFactory == null)
                    countExpression = null;
                else
                    countExpression = queryContext.CreateCountExpression(expression);

                cacheContext = queryContext.CreateCacheContext(parameterVisitor.ConstantToParameterMapper);
                queryCache.AddQuery(cacheContext, expression, countExpression, queryContext.EntryFactory);
                parameterValues = parameterVisitor.ParameterValues;
            }
            else
            {
                expression = (Expression)queryCacheItem.Query;
                queryContext.EntryFactory = queryCacheItem.EntryFactory;
                countExpression = queryCacheItem.CountExpression;
                parameterValues = cacheContext.ParameterValues;
            }

            expression = new OeParameterToVariableVisitor().Translate(expression, parameterValues);
            expression = queryContext.TranslateSource(dbContext, expression);

            if (queryContext.IsQueryCount() && countExpression != null)
            {
                countExpression = (MethodCallExpression)queryContext.TranslateSource(dbContext, countExpression);
                countExpression = (MethodCallExpression)new OeParameterToVariableVisitor().Translate(countExpression, parameterValues);
            }
            else
                countExpression = null;

            return expression;
        }
        public override Task<int> SaveChangesAsync(Object dataContext, CancellationToken cancellationToken)
        {
            var dataConnection = (T)dataContext;
            int count = dataConnection.DataContext.SaveChanges(dataConnection);
            return Task.FromResult(count);
        }
        protected override void SetEdmModel(IEdmModel edmModel)
        {
            _edmModel = edmModel;
        }

        public override Type DataContextType => typeof(T);
        public override Db.OeEntitySetAdapterCollection EntitySetAdapters => _entitySetAdapters;
    }
}
