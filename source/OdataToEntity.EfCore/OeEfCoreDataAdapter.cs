using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.OData.Edm;
using OdataToEntity.Parsers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity.EfCore
{
    public class OeEfCoreDataAdapter<T> : Db.OeDataAdapter where T : DbContext
    {
        private interface IFromSql
        {
            IQueryable FromSql(Object dataContext, String sql, Object[] parameters);
        }

        private sealed class DbSetAdapterImpl<TEntity> : Db.OeEntitySetMetaAdapter, IFromSql where TEntity : class
        {
            private readonly String _entitySetName;
            private readonly Func<T, DbSet<TEntity>> _getEntitySet;
            private IKey _key;
            private IClrPropertyGetter[] _keyGetters;
            private IProperty[] _properties;
            private IForeignKey _selfReferenceKey;

            public DbSetAdapterImpl(Func<T, DbSet<TEntity>> getEntitySet, String entitySetName)
            {
                _getEntitySet = getEntitySet;
                _entitySetName = entitySetName;
            }

            public override void AddEntity(Object dataContext, Object entity)
            {
                var context = (T)dataContext;
                DbSet<TEntity> dbSet = _getEntitySet(context);
                EntityEntry<TEntity> entry = dbSet.Add((TEntity)entity);

                InitKey(context);
                for (int i = 0; i < _key.Properties.Count; i++)
                {
                    IProperty property = _key.Properties[i];
                    if (property.ValueGenerated == ValueGenerated.OnAdd)
                        entry.GetInfrastructure().MarkAsTemporary(property);
                }
            }
            public override void AttachEntity(Object dataContext, Object entity)
            {
                AttachEntity(dataContext, entity, EntityState.Modified);
            }
            private void AttachEntity(Object dataContext, Object entity, EntityState entityState)
            {
                var context = (T)dataContext;
                InternalEntityEntry internalEntry = GetEntityEntry(context, entity);
                if (internalEntry == null)
                {
                    DbSet<TEntity> dbSet = _getEntitySet(context);
                    dbSet.Attach((TEntity)entity);
                    context.Entry(entity).State = entityState;
                }
                else
                {
                    if (entityState == EntityState.Modified)
                        foreach (IProperty property in _properties)
                        {
                            Object value = property.GetGetter().GetClrValue(entity);
                            internalEntry.SetCurrentValue(property, value);
                        }
                    else
                        internalEntry.SetEntityState(entityState);
                }
            }
            public IQueryable FromSql(Object dataContext, String sql, Object[] parameters)
            {
                DbSet<TEntity> dbSet = _getEntitySet((T)dataContext);
                return dbSet.FromSql<TEntity>(sql, parameters);
            }
            public override IQueryable GetEntitySet(Object dataContext)
            {
                return _getEntitySet((T)dataContext);
            }
            private InternalEntityEntry GetEntityEntry(T context, Object entity)
            {
                InitKey(context);
                var buffer = new ValueBuffer(GetKeyValues((TEntity)entity));
                var stateManager = (IInfrastructure<IStateManager>)context.ChangeTracker;
                return stateManager.Instance.TryGetEntry(_key, buffer, false);
            }
            private Object[] GetKeyValues(TEntity entity)
            {
                var keyValues = new Object[_keyGetters.Length];
                for (int i = 0; i < keyValues.Length; i++)
                    keyValues[i] = _keyGetters[i].GetClrValue(entity);
                return keyValues;
            }
            public override void RemoveEntity(Object dataContext, Object entity)
            {
                var context = (T)dataContext;
                InternalEntityEntry internalEntry = GetEntityEntry(context, entity);
                if (internalEntry == null)
                {
                    DbSet<TEntity> dbSet = _getEntitySet(context);
                    if (_selfReferenceKey == null)
                        dbSet.Attach((TEntity)entity);
                    else
                        entity = dbSet.Find(GetKeyValues((TEntity)entity));
                    context.Entry(entity).State = EntityState.Deleted;
                }
                else
                    internalEntry.SetEntityState(EntityState.Deleted);
            }
            private void InitKey(T context)
            {
                if (_keyGetters == null)
                {
                    IEntityType entityType = context.Model.FindEntityType(EntityType);
                    foreach (IForeignKey fkey in entityType.GetForeignKeys())
                        if (fkey.IsSelfReferencing())
                        {
                            Volatile.Write(ref _selfReferenceKey, fkey);
                            break;
                        }

                    Volatile.Write(ref _key, entityType.FindPrimaryKey());
                    Volatile.Write(ref _properties, entityType.GetProperties().Where(p => !p.IsPrimaryKey()).ToArray());
                    Volatile.Write(ref _keyGetters, _key.Properties.Select(k => k.GetGetter()).ToArray());
                }
            }

            public override Type EntityType
            {
                get
                {
                    return typeof(TEntity);
                }
            }
            public override String EntitySetName
            {
                get
                {
                    return _entitySetName;
                }
            }
        }

        private readonly static Db.OeEntitySetMetaAdapterCollection _entitySetMetaAdapters = CreateEntitySetMetaAdapters();
        private static MethodInfo[] _operations;

        public OeEfCoreDataAdapter() : this(null)
        {
        }
        public OeEfCoreDataAdapter(Db.OeQueryCache queryCache) : base(queryCache)
        {
        }

        public override void CloseDataContext(Object dataContext)
        {
            var dbContext = (T)dataContext;
            dbContext.Dispose();
        }
        public override Object CreateDataContext()
        {
            T dbContext = Db.FastActivator.CreateInstance<T>();
            dbContext.ChangeTracker.AutoDetectChangesEnabled = false;
            return dbContext;
        }
        private static Db.OeEntitySetMetaAdapterCollection CreateEntitySetMetaAdapters()
        {
            var entitySetMetaAdapters = new List<Db.OeEntitySetMetaAdapter>();
            foreach (PropertyInfo property in typeof(T).GetTypeInfo().GetProperties())
            {
                Type dbSetType = property.PropertyType.GetTypeInfo().GetInterface(typeof(IQueryable<>).FullName);
                if (dbSetType != null)
                    entitySetMetaAdapters.Add(CreateDbSetInvoker(property, dbSetType));
            }

            return new Db.OeEntitySetMetaAdapterCollection(entitySetMetaAdapters.ToArray(), new ModelBuilder.OeEdmModelMetadataProvider());
        }
        private static Db.OeEntitySetMetaAdapter CreateDbSetInvoker(PropertyInfo property, Type dbSetType)
        {
            MethodInfo mi = ((Func<PropertyInfo, Db.OeEntitySetMetaAdapter>)CreateDbSetInvoker<Object>).GetMethodInfo().GetGenericMethodDefinition();
            Type entityType = dbSetType.GetTypeInfo().GetGenericArguments()[0];
            MethodInfo func = mi.GetGenericMethodDefinition().MakeGenericMethod(entityType);
            return (Db.OeEntitySetMetaAdapter)func.Invoke(null, new Object[] { property });
        }
        private static Db.OeEntitySetMetaAdapter CreateDbSetInvoker<TEntity>(PropertyInfo property) where TEntity : class
        {
            var getDbSet = (Func<T, DbSet<TEntity>>)property.GetGetMethod().CreateDelegate(typeof(Func<T, DbSet<TEntity>>));
            return new DbSetAdapterImpl<TEntity>(getDbSet, property.Name);
        }
        public override Db.OeEntityAsyncEnumerator ExecuteEnumerator(Object dataContext, OeParseUriContext parseUriContext, CancellationToken cancellationToken)
        {
            IEnumerable<Object> enumerable;
            if (base.QueryCache.AllowCache)
                enumerable = GetFromCache<Object>(parseUriContext, (T)dataContext, base.QueryCache);
            else
                enumerable = ((IQueryable<Object>)base.CreateQuery(parseUriContext, dataContext, new OeConstantToVariableVisitor()));
            return new OeEfCoreEntityAsyncEnumeratorAdapter(enumerable, cancellationToken);
        }
        public override Db.OeEntityAsyncEnumerator ExecuteProcedure(Object dataContext, String procedureName, IReadOnlyList<KeyValuePair<String, Object>> parameters, Type returnType)
        {
            var dbContext = (T)dataContext;
            var parameterNameGenerator = dbContext.GetService<IParameterNameGeneratorFactory>().Create();
            var sqlHelper = dbContext.GetService<ISqlGenerationHelper>();

            var sql = new StringBuilder(procedureName);
            for (int i = 0; i < parameters.Count; i++)
            {
                if (i == 0)
                    sql.Append(' ');

                String name = parameterNameGenerator.GenerateNext();
                sqlHelper.GenerateParameterName(sql, name);

                if (i < parameters.Count - 1)
                    sql.Append(',');
            }

            Object[] parameterValues = null;
            if (parameters.Count > 0)
            {
                parameterValues = new Object[parameters.Count];
                for (int i = 0; i < parameterValues.Length; i++)
                    parameterValues[i] = parameters[i].Value;
            }

            if (returnType == null)
            {
                int count = dbContext.Database.ExecuteSqlCommand(sql.ToString());
                return new OeEfCoreEntityAsyncEnumeratorAdapter(new[] { (Object)count }, CancellationToken.None);
            }

            var fromSql = (IFromSql)EntitySetMetaAdapters.FindByClrType(returnType);
            if (fromSql == null)
                throw new NotSupportedException("supported only Entity type");

            var query = (IQueryable<Object>)fromSql.FromSql(dataContext, sql.ToString(), parameterValues);
            return new OeEfCoreEntityAsyncEnumeratorAdapter(query, CancellationToken.None);
        }
        public override TResult ExecuteScalar<TResult>(Object dataContext, OeParseUriContext parseUriContext)
        {
            if (base.QueryCache.AllowCache)
                return GetFromCache<TResult>(parseUriContext, (T)dataContext, base.QueryCache).Single();

            IQueryable query = parseUriContext.EntitySetAdapter.GetEntitySet(dataContext);
            Expression expression = parseUriContext.CreateExpression(query, new OeConstantToVariableVisitor());
            return query.Provider.Execute<TResult>(expression);
        }
        public override Db.OeEntitySetAdapter GetEntitySetAdapter(String entitySetName)
        {
            return new Db.OeEntitySetAdapter(_entitySetMetaAdapters.FindByEntitySetName(entitySetName), this);
        }
        private static IEnumerable<TResult> GetFromCache<TResult>(OeParseUriContext parseUriContext, T dbContext, Db.OeQueryCache queryCache)
        {
            Db.QueryCacheItem queryCacheItem = queryCache.GetQuery(parseUriContext);

            Func<QueryContext, IEnumerable<TResult>> queryExecutor;
            if (queryCacheItem == null)
            {
                IQueryable query = parseUriContext.EntitySetAdapter.GetEntitySet(dbContext);
                var parameterVisitor = new OeConstantToParameterVisitor();
                Expression expression = parseUriContext.CreateExpression(query, parameterVisitor);
                queryExecutor = dbContext.CreateQueryExecutor<TResult>(expression);

                queryCache.AddQuery(parseUriContext, queryExecutor, parameterVisitor.ConstantToParameterMapper);
                parseUriContext.ParameterValues = parameterVisitor.ParameterValues;
            }
            else
            {
                queryExecutor = (Func<QueryContext, IEnumerable<TResult>>)queryCacheItem.Query;
                parseUriContext.EntryFactory = queryCacheItem.EntryFactory;
            }

            var queryContextFactory = dbContext.GetService<IQueryContextFactory>();
            var queryContext = queryContextFactory.Create();
            foreach (Db.OeQueryCacheDbParameterValue parameterValue in parseUriContext.ParameterValues)
                queryContext.AddParameter(parameterValue.ParameterName, parameterValue.ParameterValue);

            return queryExecutor(queryContext);
        }
        public override MethodInfo[] GetOperations()
        {
            if (_operations == null)
            {
                MethodInfo[] operations = GetOperationsCore();
                Interlocked.CompareExchange(ref _operations, operations, null);
            }

            return _operations;
        }
        protected virtual MethodInfo[] GetOperationsCore()
        {
            var methodInfos = new List<MethodInfo>();
            foreach (MethodInfo methodInfo in typeof(T).GetTypeInfo().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                if (!methodInfo.IsSpecialName)
                {
                    if (methodInfo.IsVirtual && methodInfo.GetBaseDefinition().DeclaringType != typeof(T))
                        continue;
                    methodInfos.Add(methodInfo);
                }
            return methodInfos.ToArray();
        }
        public override Task<int> SaveChangesAsync(IEdmModel edmModel, Object dataContext, CancellationToken cancellationToken)
        {
            var dbContext = (T)dataContext;
            return dbContext.SaveChangesAsync(cancellationToken);
        }

        public override Db.OeEntitySetMetaAdapterCollection EntitySetMetaAdapters => _entitySetMetaAdapters;
    }
}
