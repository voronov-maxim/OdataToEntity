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

    public class OeLinq2DbDataAdapter<T> : Db.OeDataAdapter where T : DataConnection
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

        private sealed class TableAdapterImpl<TEntity> : Db.OeEntitySetMetaAdapter where TEntity : class
        {
            private readonly String _entitySetName;
            private readonly Func<T, ITable<TEntity>> _getEntitySet;

            public TableAdapterImpl(Func<T, ITable<TEntity>> getEntitySet, String entitySetName)
            {
                _getEntitySet = getEntitySet;
                _entitySetName = entitySetName;
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
                var dc = dataContext as IOeLinq2DbDataContext;
                if (dc == null)
                    throw new InvalidOperationException(dataContext.GetType().ToString() + "must implement " + nameof(IOeLinq2DbDataContext));

                if (dc.DataContext == null)
                    dc.DataContext = new OeLinq2DbDataContext();
                return dc.DataContext.GetTable<TEntity>();
            }
            public override IQueryable GetEntitySet(Object dataContext) => _getEntitySet((T)dataContext);
            public override void RemoveEntity(Object dataContext, ODataResourceBase entry)
            {
                var entity = (TEntity)OeEdmClrHelper.CreateEntity(EntityType, entry);
                GetTable(dataContext).Delete(entity);
            }

            public override Type EntityType => typeof(TEntity);
            public override String EntitySetName => _entitySetName;
        }

        private readonly static Db.OeEntitySetMetaAdapterCollection _entitySetMetaAdapters = CreateEntitySetMetaAdapters();

        public OeLinq2DbDataAdapter() : this(null)
        {
        }
        public OeLinq2DbDataAdapter(Db.OeQueryCache queryCache)
            : base(queryCache, new OeLinq2DbOperationAdapter(typeof(T)))
        {
        }

        public override void CloseDataContext(Object dataContext)
        {
            var dbContext = (T)dataContext;
            dbContext.Dispose();
        }
        public override Object CreateDataContext()
        {
            return Db.FastActivator.CreateInstance<T>();
        }
        private static Db.OeEntitySetMetaAdapterCollection CreateEntitySetMetaAdapters()
        {
            InitializeLinq2Db();

            var entitySetMetaAdapters = new List<Db.OeEntitySetMetaAdapter>();
            foreach (PropertyInfo property in typeof(T).GetTypeInfo().GetProperties())
            {
                Type entitySetType = property.PropertyType.GetTypeInfo().GetInterface(typeof(IQueryable<>).FullName);
                if (entitySetType != null)
                    entitySetMetaAdapters.Add(CreateDbSetInvoker(property, entitySetType));
            }
            return new Db.OeEntitySetMetaAdapterCollection(entitySetMetaAdapters.ToArray(), new OeLinq2DbEdmModelMetadataProvider());
        }
        private static Db.OeEntitySetMetaAdapter CreateDbSetInvoker(PropertyInfo property, Type entitySetType)
        {
            MethodInfo mi = ((Func<PropertyInfo, Db.OeEntitySetMetaAdapter>)CreateEntitySetInvoker<Object>).GetMethodInfo().GetGenericMethodDefinition();
            Type entityType = entitySetType.GetTypeInfo().GetGenericArguments()[0];
            MethodInfo func = mi.GetGenericMethodDefinition().MakeGenericMethod(entityType);
            return (Db.OeEntitySetMetaAdapter)func.Invoke(null, new Object[] { property });
        }
        private static Db.OeEntitySetMetaAdapter CreateEntitySetInvoker<TEntity>(PropertyInfo property) where TEntity : class
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
                expression = OeQueryContext.TranslateSource(entitySet.Expression, expression);
                expression = new ParameterVisitor().Visit(expression);

                if (queryContext.ODataUri.QueryCount.GetValueOrDefault())
                    countExpression = OeQueryContext.CreateCountExpression(expression);
            }

            IQueryable<Object> query = (IQueryable<Object>)entitySet.Provider.CreateQuery(expression);
            Db.OeAsyncEnumerator asyncEnumerator = new Db.OeAsyncEnumeratorAdapter(query, cancellationToken);

            if (countExpression != null)
                asyncEnumerator.Count = entitySet.Provider.Execute<int>(countExpression);

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
                expression = OeQueryContext.TranslateSource(query.Expression, expression);
                expression = new ParameterVisitor().Visit(expression);
            }
            return query.Provider.Execute<TResult>(expression);
        }
        public override Db.OeEntitySetAdapter GetEntitySetAdapter(String entitySetName)
        {
            return new Db.OeEntitySetAdapter(EntitySetMetaAdapters.FindByEntitySetName(entitySetName), this);
        }
        private static Expression GetFromCache(OeQueryContext queryContext, T dbContext, Db.OeQueryCache queryCache,
            out MethodCallExpression countExpression)
        {
            OeCacheContext cacheContext = queryContext.CreateCacheContext();
            Db.QueryCacheItem queryCacheItem = queryCache.GetQuery(cacheContext);

            Expression expression;
            IReadOnlyList<Db.OeQueryCacheDbParameterValue> parameterValues;
            IQueryable query = queryContext.EntitySetAdapter.GetEntitySet(dbContext);
            if (queryCacheItem == null)
            {
                var parameterVisitor = new OeConstantToParameterVisitor();
                expression = queryContext.CreateExpression(parameterVisitor);
                expression = new ParameterVisitor().Visit(expression);

                countExpression = OeQueryContext.CreateCountExpression(expression);
                queryCache.AddQuery(queryContext.CreateCacheContext(parameterVisitor.ConstantToParameterMapper), expression, null,
                    queryContext.EntryFactory, queryContext.SkipTokenParser?.Accessors);
                parameterValues = parameterVisitor.ParameterValues;
            }
            else
            {
                expression = (Expression)queryCacheItem.Query;
                queryContext.EntryFactory = queryCacheItem.EntryFactory;
                if (queryContext.SkipTokenParser != null)
                    queryContext.SkipTokenParser.Accessors = queryCacheItem.SkipTokenAccessors;
                countExpression = queryCacheItem.CountExpression;
                parameterValues = cacheContext.ParameterValues;
            }

            expression = new OeParameterToVariableVisitor().Translate(expression, parameterValues);
            expression = OeQueryContext.TranslateSource(query.Expression, expression);

            if (queryContext.ODataUri.QueryCount.GetValueOrDefault())
            {
                countExpression = (MethodCallExpression)OeQueryContext.TranslateSource(query.Expression, countExpression);
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
        public override Task<int> SaveChangesAsync(IEdmModel edmModel, Object dataContext, CancellationToken cancellationToken)
        {
            var dataConnection = (DataConnection)dataContext;
            OeLinq2DbDataContext dc = ((IOeLinq2DbDataContext)dataConnection).DataContext;
            int count = dc.SaveChanges(edmModel, EntitySetMetaAdapters, dataConnection);
            return Task.FromResult(count);
        }

        public override Db.OeEntitySetMetaAdapterCollection EntitySetMetaAdapters => _entitySetMetaAdapters;
    }
}
