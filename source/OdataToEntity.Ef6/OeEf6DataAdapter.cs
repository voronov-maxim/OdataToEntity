using Microsoft.OData.Edm;
using OdataToEntity.Parsers;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Core;
using System.Data.Entity.Core.Metadata.Edm;
using System.Data.Entity.Core.Objects;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity.Ef6
{
    public class OeEf6DataAdapter<T> : Db.OeDataAdapter where T : DbContext
    {
        private sealed class DbSetAdapterImpl<TEntity> : Db.OeEntitySetMetaAdapter where TEntity : class
        {
            private readonly String _entitySetName;
            private readonly Func<T, IDbSet<TEntity>> _getEntitySet;
            private bool _isCascade;
            private bool _isInitialized;
            private bool _isSelfReference;

            public DbSetAdapterImpl(Func<T, IDbSet<TEntity>> getEntitySet, String entitySetName)
            {
                _getEntitySet = getEntitySet;
                _entitySetName = entitySetName;
            }

            public override void AddEntity(Object dataContext, Object entity)
            {
                IDbSet<TEntity> dbSet = _getEntitySet((T)dataContext);
                dbSet.Add((TEntity)entity);
            }
            public override void AttachEntity(Object dataContext, Object entity)
            {
                AttachEntity(dataContext, entity, EntityState.Modified);
            }
            private void AttachEntity(Object dataContext, Object entity, EntityState entityState)
            {
                ObjectContext objectContext = ((IObjectContextAdapter)dataContext).ObjectContext;
                EntityKey entityKey = objectContext.CreateEntityKey(EntitySetName, entity);

                ObjectStateEntry objectStateEntry;
                if (objectContext.ObjectStateManager.TryGetObjectStateEntry(entityKey, out objectStateEntry))
                {
                    if (entityState == EntityState.Modified)
                        objectStateEntry.ApplyCurrentValues(entity);
                    else
                        objectStateEntry.ChangeState(entityState);
                }
                else
                {
                    var context = (T)dataContext;
                    IDbSet<TEntity> dbSet = _getEntitySet(context);
                    dbSet.Attach((TEntity)entity);
                    context.Entry(entity).State = entityState;
                }
            }
            public override IQueryable GetEntitySet(Object dataContext)
            {
                return _getEntitySet((T)dataContext);
            }
            private void InitKey(T context)
            {
                if (_isInitialized)
                    return;

                ObjectContext objectContext = ((IObjectContextAdapter)context).ObjectContext;
                var itemCollection = (ObjectItemCollection)objectContext.MetadataWorkspace.GetItemCollection(DataSpace.OSpace);
                EntityType entityType = itemCollection.OfType<EntityType>().Single(e => itemCollection.GetClrType(e) == typeof(TEntity));

                bool isCascade = true;
                foreach (AssociationType associationType in objectContext.MetadataWorkspace.GetItems<AssociationType>(DataSpace.CSpace))
                    if (associationType.Constraint.ToRole.GetEntityType().Name == typeof(TEntity).Name &&
                        associationType.Constraint.ToRole.DeleteBehavior == OperationAction.None)
                        {
                            isCascade = false;
                            break;
                        }

                bool isSelfReference = false;
                foreach (NavigationProperty navigationProperty in entityType.NavigationProperties)
                    if (navigationProperty.FromEndMember.GetEntityType() == navigationProperty.ToEndMember.GetEntityType())
                    {
                        isSelfReference = true;
                        break;
                    }

                Volatile.Write(ref _isCascade, isCascade);
                Volatile.Write(ref _isSelfReference, isSelfReference);
                Volatile.Write(ref _isInitialized, true);
            }
            public override void RemoveEntity(Object dataContext, Object entity)
            {
                var context = (T)dataContext;
                InitKey(context);

                ObjectContext objectContext = ((IObjectContextAdapter)context).ObjectContext;
                EntityKey entityKey = objectContext.CreateEntityKey(EntitySetName, entity);

                ObjectStateEntry objectStateEntry;
                if (objectContext.ObjectStateManager.TryGetObjectStateEntry(entityKey, out objectStateEntry))
                    objectStateEntry.ChangeState(EntityState.Deleted);
                else
                {
                    if (_isCascade && !_isSelfReference)
                        context.Entry(entity).State = EntityState.Deleted;
                    else
                    {
                        var keyValues = new Object[entityKey.EntityKeyValues.Length];
                        for (int i = 0; i < keyValues.Length; i++)
                            keyValues[i] = entityKey.EntityKeyValues[i].Value;

                        IDbSet<TEntity> dbSet = _getEntitySet(context);
                        context.Entry(dbSet.Find(keyValues)).State = EntityState.Deleted;
                    }
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

        private static DummyCommandBuilder _dummyCommandBuilder;
        private readonly static Db.OeEntitySetMetaAdapterCollection _entitySetMetaAdapters = CreateEntitySetMetaAdapters();
        private static MethodInfo[] _operations;

        public OeEf6DataAdapter() : base(null)
        {
        }
        public OeEf6DataAdapter(Db.OeQueryCache queryCache) : base(queryCache)
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
            dbContext.Configuration.AutoDetectChangesEnabled = false;
            dbContext.Configuration.ProxyCreationEnabled = false;
            return dbContext;
        }
        private static Db.OeEntitySetMetaAdapterCollection CreateEntitySetMetaAdapters()
        {
            var entitySetMetaAdapters = new List<Db.OeEntitySetMetaAdapter>();
            foreach (PropertyInfo property in typeof(T).GetProperties())
            {
                Type dbSetType = property.PropertyType.GetInterface(typeof(IDbSet<>).FullName);
                if (dbSetType != null)
                    entitySetMetaAdapters.Add(CreateDbSetInvoker(property, dbSetType));
            }
            return new Db.OeEntitySetMetaAdapterCollection(entitySetMetaAdapters.ToArray(), new ModelBuilder.OeEdmModelMetadataProvider());
        }
        private static Db.OeEntitySetMetaAdapter CreateDbSetInvoker(PropertyInfo property, Type dbSetType)
        {
            MethodInfo mi = ((Func<PropertyInfo, Db.OeEntitySetMetaAdapter>)CreateDbSetInvoker<Object>).Method.GetGenericMethodDefinition();
            Type entityType = dbSetType.GetGenericArguments()[0];
            MethodInfo func = mi.GetGenericMethodDefinition().MakeGenericMethod(entityType);
            return (Db.OeEntitySetMetaAdapter)func.Invoke(null, new Object[] { property });
        }
        private static Db.OeEntitySetMetaAdapter CreateDbSetInvoker<TEntity>(PropertyInfo property) where TEntity : class
        {
            var getDbSet = (Func<T, IDbSet<TEntity>>)Delegate.CreateDelegate(typeof(Func<T, IDbSet<TEntity>>), property.GetGetMethod());
            return new DbSetAdapterImpl<TEntity>(getDbSet, property.Name);
        }
        public override Db.OeEntityAsyncEnumerator ExecuteEnumerator(Object dataContext, OeParseUriContext parseUriContext, CancellationToken cancellationToken)
        {
            IQueryable query = parseUriContext.EntitySetAdapter.GetEntitySet(dataContext);
            Expression expression = parseUriContext.CreateExpression(query, new OeConstantToVariableVisitor());

            expression = new EnumerableToQuerableVisitor().Visit(expression);
            var queryAsync = (IDbAsyncEnumerable)query.Provider.CreateQuery(expression);
            return new OeEf6EntityAsyncEnumerator(queryAsync.GetAsyncEnumerator(), cancellationToken);
        }
        public override Db.OeEntityAsyncEnumerator ExecuteProcedure(Object dataContext, String procedureName, IReadOnlyList<KeyValuePair<String, Object>> parameters, Type returnType)
        {
            var dbContext = (T)dataContext;

            var sql = new StringBuilder(procedureName);
            for (int i = 0; i < parameters.Count; i++)
            {
                if (i == 0)
                    sql.Append(' ');

                sql.Append(GetDbParameterName(dbContext, i));
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
                int count = dbContext.Database.ExecuteSqlCommand(sql.ToString(), parameterValues);
                return new Db.OeEntityAsyncEnumeratorAdapter(new Object[] { count }, CancellationToken.None);
            }
            else
            {
                DbRawSqlQuery query = dbContext.Database.SqlQuery(returnType, sql.ToString(), parameterValues);
                return new Db.OeEntityAsyncEnumeratorAdapter(query, CancellationToken.None);
            }
        }
        public override TResult ExecuteScalar<TResult>(Object dataContext, OeParseUriContext parseUriContext)
        {
            IQueryable query = parseUriContext.EntitySetAdapter.GetEntitySet(dataContext);
            Expression expression = parseUriContext.CreateExpression(query, new OeConstantToVariableVisitor());
            return query.Provider.Execute<TResult>(expression);
        }
        private static String GetDbParameterName(DbContext dbContext, int parameterOrder)
        {
            if (_dummyCommandBuilder == null)
                Volatile.Write(ref _dummyCommandBuilder, new DummyCommandBuilder(dbContext.Database.Connection));
            return _dummyCommandBuilder.GetDbParameterName(parameterOrder);
        }
        public override Db.OeEntitySetAdapter GetEntitySetAdapter(String entitySetName)
        {
            return new Db.OeEntitySetAdapter(_entitySetMetaAdapters.FindByEntitySetName(entitySetName), this);
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

        public override Db.OeEntitySetMetaAdapterCollection EntitySetMetaAdapters
        {
            get
            {
                return _entitySetMetaAdapters;
            }
        }
    }
}
