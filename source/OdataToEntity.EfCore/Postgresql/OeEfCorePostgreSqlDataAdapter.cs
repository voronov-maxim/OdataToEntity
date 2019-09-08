using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace OdataToEntity.EfCore.Postgresql
{
    public class OeEfCorePostgreSqlDataAdapter<T> : OeEfCoreDataAdapter<T> where T : DbContext
    {
        public OeEfCorePostgreSqlDataAdapter() : this(null, null)
        {
        }
        public OeEfCorePostgreSqlDataAdapter(DbContextOptions options, Cache.OeQueryCache queryCache)
            : this(options, queryCache, new OePostgreSqlEfCoreOperationAdapter(typeof(T)))
        {
        }
        protected OeEfCorePostgreSqlDataAdapter(DbContextOptions options, Cache.OeQueryCache queryCache, OeEfCoreOperationAdapter operationAdapter)
            : base(options, queryCache, operationAdapter)
        {
            base.IsDatabaseNullHighestValue = true;
        }

        protected override Expression TranslateExpression(Expression expression)
        {
            return new OeDateTimeOffsetMembersVisitor().Visit(expression);
        }
    }
}
