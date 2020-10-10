using Microsoft.EntityFrameworkCore;
using Microsoft.OData.Edm;
using System.Linq.Expressions;

namespace OdataToEntity.EfCore.Postgresql
{
    public class OeEfCorePostgreSqlDataAdapter<T> : OeEfCoreDataAdapter<T> where T : DbContext
    {
        public OeEfCorePostgreSqlDataAdapter() : this(null, null)
        {
        }
        public OeEfCorePostgreSqlDataAdapter(DbContextOptions<T>? options, Cache.OeQueryCache? queryCache)
            : this(options, queryCache, new OePostgreSqlEfCoreOperationAdapter(typeof(T)))
        {
        }
        protected OeEfCorePostgreSqlDataAdapter(DbContextOptions<T>? options, Cache.OeQueryCache? queryCache, OeEfCoreOperationAdapter operationAdapter)
            : base(options, queryCache, operationAdapter)
        {
            base.IsDatabaseNullHighestValue = true;
        }

        protected override Expression TranslateExpression(IEdmModel edmModel, Expression expression)
        {
            expression = new OeDateTimeOffsetMembersVisitor().Visit(expression);
            return base.TranslateExpression(edmModel, expression);
        }
    }
}
