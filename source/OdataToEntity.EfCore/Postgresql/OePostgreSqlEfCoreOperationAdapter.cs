using System;
using System.Collections.Generic;
using System.Threading;

namespace OdataToEntity.EfCore.Postgresql
{
    public class OePostgreSqlEfCoreOperationAdapter : OeEfCoreOperationAdapter
    {
        public OePostgreSqlEfCoreOperationAdapter(Type dataContextType) : base(dataContextType, true)
        {
        }

        public override IAsyncEnumerable<Object> ExecuteProcedureNonQuery(Object dataContext, String operationName, IReadOnlyList<KeyValuePair<String, Object?>> parameters)
        {
            return base.ExecuteFunctionNonQuery(dataContext, operationName, parameters);
        }
        public override IAsyncEnumerable<Object> ExecuteProcedureReader(Object dataContext, String operationName, IReadOnlyList<KeyValuePair<String, Object?>> parameters, Db.OeEntitySetAdapter entitySetAdapter)
        {
            return base.ExecuteFunctionReader(dataContext, operationName, parameters, entitySetAdapter);
        }
        public override IAsyncEnumerable<Object> ExecuteProcedurePrimitive(Object dataContext, String operationName,
            IReadOnlyList<KeyValuePair<String, Object?>> parameters, Type returnType, CancellationToken cancellationToken)
        {
            return base.ExecuteFunctionPrimitive(dataContext, operationName, parameters, returnType, cancellationToken);
        }
    }
}
