using LinqToDB;
using LinqToDB.Data;
using Microsoft.OData.Edm;
using OdataToEntity.Db;
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

    public class OeLinq2DbDataAdapter<T> : OeDataAdapter where T : DataConnection
    {
        private sealed class ParameterVisitor : ExpressionVisitor
        {
            private readonly Dictionary<ParameterExpression, ParameterExpression> _parameters;

            public ParameterVisitor()
            {
                _parameters = new Dictionary<ParameterExpression, ParameterExpression>();
            }

            protected override Expression VisitParameter(ParameterExpression node)
            {
                ParameterExpression parameter;
                if (_parameters.TryGetValue(node, out parameter))
                    return parameter;

                parameter = Expression.Parameter(node.Type, node.Name ?? node.ToString());
                _parameters.Add(node, parameter);
                return parameter;
            }
            protected override Expression VisitNew(NewExpression node)
            {
                var arguments = new Expression[node.Arguments.Count];
                for (int i = 0; i < arguments.Length; i++)
                {
                    Expression argument = base.Visit(node.Arguments[i]);
                    var call = argument as MethodCallExpression;
                    if (call != null && call.Type.GetTypeInfo().IsGenericType && call.Type.GetGenericTypeDefinition() == typeof(IOrderedEnumerable<>))
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
        }

        private sealed class TableAdapterImpl<TEntity> : OeEntitySetMetaAdapter where TEntity : class
        {
            private readonly String _entitySetName;
            private readonly Func<T, ITable<TEntity>> _getEntitySet;

            public TableAdapterImpl(Func<T, ITable<TEntity>> getEntitySet, String entitySetName)
            {
                _getEntitySet = getEntitySet;
                _entitySetName = entitySetName;
            }

            public override void AddEntity(Object dataContext, Object entity)
            {
                GetTable(dataContext).Insert((TEntity)entity);
            }
            public override void AttachEntity(Object dataContext, Object entity)
            {
                GetTable(dataContext).Update((TEntity)entity);
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
            public override IQueryable GetEntitySet(Object dataContext)
            {
                return _getEntitySet((T)dataContext);
            }
            public override void RemoveEntity(Object dataContext, Object entity)
            {
                GetTable(dataContext).Delete((TEntity)entity);
            }

            public override Type EntityType => typeof(TEntity);
            public override String EntitySetName => _entitySetName;
        }

        private readonly static Lazy<OeEntitySetMetaAdapterCollection> _entitySetMetaAdapters = new Lazy<OeEntitySetMetaAdapterCollection>(CreateEntitySetMetaAdapters);
 
        public OeLinq2DbDataAdapter() : this(null)
        {
        }
        public OeLinq2DbDataAdapter(Db.OeQueryCache queryCache) : base(queryCache)
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
        private static OeEntitySetMetaAdapterCollection CreateEntitySetMetaAdapters()
        {
            InitializeLinq2Db();

            var entitySetMetaAdapters = new List<OeEntitySetMetaAdapter>();
            foreach (PropertyInfo property in typeof(T).GetTypeInfo().GetProperties())
            {
                Type entitySetType = property.PropertyType.GetTypeInfo().GetInterface(typeof(IQueryable<>).FullName);
                if (entitySetType != null)
                    entitySetMetaAdapters.Add(CreateDbSetInvoker(property, entitySetType));
            }
            return new OeEntitySetMetaAdapterCollection(entitySetMetaAdapters.ToArray(), new OeLinq2DbEdmModelMetadataProvider());
        }
        private static OeEntitySetMetaAdapter CreateDbSetInvoker(PropertyInfo property, Type entitySetType)
        {
            MethodInfo mi = ((Func<PropertyInfo, OeEntitySetMetaAdapter>)CreateEntitySetInvoker<Object>).GetMethodInfo().GetGenericMethodDefinition();
            Type entityType = entitySetType.GetTypeInfo().GetGenericArguments()[0];
            MethodInfo func = mi.GetGenericMethodDefinition().MakeGenericMethod(entityType);
            return (OeEntitySetMetaAdapter)func.Invoke(null, new Object[] { property });
        }
        private static OeEntitySetMetaAdapter CreateEntitySetInvoker<TEntity>(PropertyInfo property) where TEntity : class
        {
            var getEntitySet = (Func<T, ITable<TEntity>>)property.GetGetMethod().CreateDelegate(typeof(Func<T, ITable<TEntity>>));
            return new TableAdapterImpl<TEntity>(getEntitySet, property.Name);
        }
        public override OeEntityAsyncEnumerator ExecuteEnumerator(Object dataContext, OeParseUriContext parseUriContext, CancellationToken cancellationToken)
        {
            IQueryable entitySet = parseUriContext.EntitySetAdapter.GetEntitySet(dataContext);
            Expression expression = parseUriContext.CreateExpression(entitySet, new OeConstantToVariableVisitor());
            expression = new ParameterVisitor().Visit(expression);
            var query = (IQueryable<Object>)entitySet.Provider.CreateQuery(expression);
            return new OeLinq2DbEntityAsyncEnumerator(query.GetEnumerator(), cancellationToken);
        }
        public override TResult ExecuteScalar<TResult>(Object dataContext, OeParseUriContext parseUriContext)
        {
            IQueryable query = parseUriContext.EntitySetAdapter.GetEntitySet(dataContext);
            Expression expression = parseUriContext.CreateExpression(query, new OeConstantToVariableVisitor());
            expression = new ParameterVisitor().Visit(expression);
            return query.Provider.Execute<TResult>(expression);
        }
        public override OeEntitySetAdapter GetEntitySetAdapter(String entitySetName)
        {
            return new OeEntitySetAdapter(EntitySetMetaAdapters.FindByEntitySetName(entitySetName), this);
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

        public override OeEntitySetMetaAdapterCollection EntitySetMetaAdapters => _entitySetMetaAdapters.Value;
    }
}
