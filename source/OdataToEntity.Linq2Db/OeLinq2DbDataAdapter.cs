using LinqToDB;
using LinqToDB.Data;
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

namespace OdataToEntity.Linq2Db
{
    internal static class SqlFunction
    {
        [Sql.Function]
        public static int DatePart(Sql.DateParts part, DateTimeOffset date) => 0;
    }

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
                if (_parameters.TryGetValue(node, out ParameterExpression parameter))
                    return parameter;

                parameter = Expression.Parameter(node.Type, node.Name ?? node.ToString());
                _parameters.Add(node, parameter);
                return parameter;
            }
        }

        private sealed class TableAdapterImpl<TEntity> : Db.OeEntitySetAdapter where TEntity : class
        {
            private readonly Func<T, ITable<TEntity>> _getEntitySet;

            public TableAdapterImpl(Func<T, ITable<TEntity>> getEntitySet, String entitySetName)
            {
                _getEntitySet = getEntitySet;
                EntitySetName = entitySetName;
            }

            public override void AddEntity(Object dataContext, ODataResourceBase entry)
            {
                var entity = (TEntity)OeEdmClrHelper.CreateEntity(EntityType, entry);
                GetTable(dataContext).Insert(entity);
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
            public override IQueryable GetEntitySet(Object dataContext) => _getEntitySet((T)dataContext);
            public override void RemoveEntity(Object dataContext, ODataResourceBase entry)
            {
                var entity = (TEntity)OeEdmClrHelper.CreateEntity(EntityType, entry);
                GetTable(dataContext).Delete(entity);
            }

            public override Type EntityType => typeof(TEntity);
            public override String EntitySetName { get; }
        }

        private IEdmModel _edmModel;
        private readonly static Db.OeEntitySetAdapterCollection _entitySetAdapters = CreateEntitySetAdapters();

        public OeLinq2DbDataAdapter() : this(null)
        {
        }
        public OeLinq2DbDataAdapter(Cache.OeQueryCache queryCache)
            : base(queryCache, new OeLinq2DbOperationAdapter(typeof(T)))
        {
        }
        public OeLinq2DbDataAdapter(Cache.OeQueryCache queryCache, Db.OeOperationAdapter operationAdapter)
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
            var dataContext = Infrastructure.FastActivator.CreateInstance<T>();
            dataContext.DataContext = new OeLinq2DbDataContext(_edmModel, _entitySetAdapters);
            return dataContext;
        }
        private static Db.OeEntitySetAdapterCollection CreateEntitySetAdapters()
        {
            InitializeLinq2Db();

            var entitySetAdapters = new List<Db.OeEntitySetAdapter>();
            foreach (PropertyInfo property in typeof(T).GetTypeInfo().GetProperties())
            {
                Type entitySetType = property.PropertyType.GetTypeInfo().GetInterface(typeof(IQueryable<>).FullName);
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
            return (Db.OeEntitySetAdapter)func.Invoke(null, new Object[] { property });
        }
        private static Db.OeEntitySetAdapter CreateEntitySetInvoker<TEntity>(PropertyInfo property) where TEntity : class
        {
            var getEntitySet = (Func<T, ITable<TEntity>>)property.GetGetMethod().CreateDelegate(typeof(Func<T, ITable<TEntity>>));
            return new TableAdapterImpl<TEntity>(getEntitySet, property.Name);
        }
        public override Db.OeAsyncEnumerator ExecuteEnumerator(Object dataContext, OeQueryContext queryContext, CancellationToken cancellationToken)
        {
            Expression expression;
            MethodCallExpression countExpression = null;
            IQueryable entitySet = queryContext.EntitySetAdapter.GetEntitySet(dataContext);
            if (base.QueryCache.AllowCache)
                expression = GetFromCache(queryContext, (T)dataContext, base.QueryCache, out countExpression);
            else
            {
                expression = queryContext.CreateExpression(new OeConstantToVariableVisitor());
                expression = queryContext.TranslateSource(dataContext, expression);
                expression = new ParameterVisitor().Visit(expression);

                if (queryContext.ODataUri.QueryCount.GetValueOrDefault())
                    countExpression = OeQueryContext.CreateCountExpression(expression);
            }

            IQueryable<Object> query = (IQueryable<Object>)entitySet.Provider.CreateQuery(expression);
            Db.OeAsyncEnumerator asyncEnumerator = new Db.OeAsyncEnumeratorAdapter(query, cancellationToken);

            if (countExpression != null)
                asyncEnumerator.Count = entitySet.Provider.Execute<int>(countExpression);

            return base.OperationAdapter.ApplyBoundFunction(asyncEnumerator, queryContext);
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
            out MethodCallExpression countExpression)
        {
            Cache.OeCacheContext cacheContext = queryContext.CreateCacheContext();
            Cache.OeQueryCacheItem queryCacheItem = queryCache.GetQuery(cacheContext);

            Expression expression;
            IReadOnlyList<Cache.OeQueryCacheDbParameterValue> parameterValues;
            IQueryable query = queryContext.EntitySetAdapter.GetEntitySet(dbContext);
            if (queryCacheItem == null)
            {
                var parameterVisitor = new OeConstantToParameterVisitor();
                expression = queryContext.CreateExpression(parameterVisitor);
                expression = new ParameterVisitor().Visit(expression);

                countExpression = OeQueryContext.CreateCountExpression(expression);
                queryCache.AddQuery(queryContext.CreateCacheContext(parameterVisitor.ConstantToParameterMapper), expression, countExpression,
                    queryContext.EntryFactory, queryContext.SkipTokenAccessors);
                parameterValues = parameterVisitor.ParameterValues;
            }
            else
            {
                expression = (Expression)queryCacheItem.Query;
                queryContext.EntryFactory = queryCacheItem.EntryFactory;
                queryContext.SkipTokenAccessors = queryCacheItem.SkipTokenAccessors;
                countExpression = queryCacheItem.CountExpression;
                parameterValues = cacheContext.ParameterValues;
            }

            expression = new OeParameterToVariableVisitor().Translate(expression, parameterValues);
            expression = queryContext.TranslateSource(dbContext, expression);

            if (queryContext.ODataUri.QueryCount.GetValueOrDefault())
            {
                countExpression = (MethodCallExpression)queryContext.TranslateSource(dbContext, countExpression);
                countExpression = (MethodCallExpression)new OeParameterToVariableVisitor().Translate(countExpression, parameterValues);
            }
            else
                countExpression = null;

            return expression;
        }
        private static void InitializeLinq2Db()
        {
            Func<Sql.DateParts, DateTimeOffset, int> datePartFunc = SqlFunction.DatePart;
            ParameterExpression parameter = Expression.Parameter(typeof(DateTimeOffset));

            foreach (Sql.DateParts datePart in Enum.GetValues(typeof(Sql.DateParts)))
            {
                PropertyInfo propertyInfo = typeof(DateTimeOffset).GetProperty(datePart.ToString());
                if (propertyInfo == null)
                    continue;

                MethodCallExpression call = Expression.Call(datePartFunc.GetMethodInfo(), Expression.Constant(datePart), parameter);
                LambdaExpression lambda = Expression.Lambda(call, parameter);
                LinqToDB.Linq.Expressions.MapMember(propertyInfo, lambda);
            }
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
