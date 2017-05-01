using Microsoft.OData.Edm;
using OdataToEntity.Parsers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity.Db
{
    public abstract class OeDataAdapter
    {
        private static MethodInfo[] _operations;
        private readonly OeQueryCache _queryCache;

        public OeDataAdapter(OeQueryCache queryCache)
        {
            _queryCache = queryCache ?? new OeQueryCache { AllowCache = false };
        }

        public abstract void CloseDataContext(Object dataContext);
        protected IQueryable CreateQuery(OeParseUriContext parseUriContext, Object dataContext, OeConstantToVariableVisitor constantToVariableVisitor)
        {
            IQueryable query = parseUriContext.EntitySetAdapter.GetEntitySet(dataContext);
            Expression expression = parseUriContext.CreateExpression(query, constantToVariableVisitor);
            return query.Provider.CreateQuery(expression);
        }
        public abstract Object CreateDataContext();
        public abstract OeEntityAsyncEnumerator ExecuteEnumerator(Object dataContext, OeParseUriContext parseUriContext, CancellationToken cancellationToken);
        public virtual OeEntityAsyncEnumerator ExecuteProcedure(Object dataContext, String procedureName, IReadOnlyList<KeyValuePair<String, Object>> parameters, Type returnType) => throw new NotImplementedException();
        public abstract TResult ExecuteScalar<TResult>(Object dataContext, OeParseUriContext parseUriContext);
        protected abstract Type GetDataContextType();
        public abstract OeEntitySetAdapter GetEntitySetAdapter(String entitySetName);
        public MethodInfo[] GetOperations()
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
            Type dataContextType = GetDataContextType();
            foreach (MethodInfo methodInfo in dataContextType.GetTypeInfo().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                if (!methodInfo.IsSpecialName)
                {
                    if (methodInfo.IsVirtual || methodInfo.IsGenericMethod || methodInfo.GetBaseDefinition().DeclaringType != dataContextType)
                        continue;
                    methodInfos.Add(methodInfo);
                }
            return methodInfos.ToArray();
        }
        public abstract Task<int> SaveChangesAsync(IEdmModel edmModel, Object dataContext, CancellationToken cancellationToken);

        protected internal OeQueryCache QueryCache => _queryCache;
        public abstract OeEntitySetMetaAdapterCollection EntitySetMetaAdapters { get; }
    }
}
