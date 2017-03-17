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
        public abstract OeEntitySetAdapter GetEntitySetAdapter(String entitySetName);
        public virtual MethodInfo[] GetOperations() => null;
        public abstract Task<int> SaveChangesAsync(IEdmModel edmModel, Object dataContext, CancellationToken cancellationToken);

        protected internal OeQueryCache QueryCache => _queryCache;
        public abstract OeEntitySetMetaAdapterCollection EntitySetMetaAdapters { get; }
    }
}
