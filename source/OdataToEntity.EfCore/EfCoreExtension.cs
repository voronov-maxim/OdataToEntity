#nullable enable
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using OdataToEntity.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace OdataToEntity.EfCore
{
    internal static class EfCoreExtension
    {
        public static Func<QueryContext, IAsyncEnumerable<T>> CreateAsyncQueryExecutor<T>(this DbContext dbContext, Expression expression)
        {
            var queryCompilationContextFactory = dbContext.GetService<IQueryCompilationContextFactory>();
            if (Parsers.OeExpressionHelper.GetCollectionItemTypeOrNull(expression.Type) == null)
            {
                Func<QueryContext, Task<T>> executor = queryCompilationContextFactory.Create(true).CreateQueryExecutor<Task<T>>(expression);
                return queryContext => AsyncEnumeratorHelper.ToAsyncEnumerable(executor(queryContext));
            }

            return queryCompilationContextFactory.Create(true).CreateQueryExecutor<IAsyncEnumerable<T>>(expression);
        }
    }
}
