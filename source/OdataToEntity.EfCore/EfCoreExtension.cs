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
    public static class EfCoreExtension
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
        public static DbContextOptions CreateOptions(this DbContextOptions options, Type dbContextType)
        {
            Type optionsBuilderType = typeof(DbContextOptionsBuilder<>).MakeGenericType(dbContextType);
            var optionsBuilder = (DbContextOptionsBuilder)Activator.CreateInstance(optionsBuilderType)!;
            return optionsBuilder.CreateOptions(options);
        }
        public static DbContextOptions CreateOptions(this DbContextOptionsBuilder optionsBuilder, DbContextOptions options)
        {
            DbContextOptions contextOptions = optionsBuilder.Options;
            foreach (IDbContextOptionsExtension extension in options.Extensions)
                if (extension is CoreOptionsExtension coreOptionsExtension)
                {
                    CoreOptionsExtension? newCoreOptions = contextOptions.FindExtension<CoreOptionsExtension>();
                    if (newCoreOptions == null)
                        contextOptions = contextOptions.WithExtension(coreOptionsExtension);
                    else
                    {
                        if (coreOptionsExtension.ReplacedServices != null)
                        {
                            foreach (KeyValuePair<(Type, Type), Type> replacedService in coreOptionsExtension.ReplacedServices)
                                newCoreOptions = newCoreOptions.WithReplacedService(replacedService.Key.Item1, replacedService.Value);
                            contextOptions = contextOptions.WithExtension(newCoreOptions);
                        }
                    }
                }
                else
                {
                    var withExtensionFunc = (Func<IDbContextOptionsExtension, DbContextOptions>)contextOptions.WithExtension<IDbContextOptionsExtension>;
                    var withExtension = withExtensionFunc.Method.GetGenericMethodDefinition().MakeGenericMethod(new[] { extension.GetType() });
                    contextOptions = (DbContextOptions)withExtension.Invoke(contextOptions, new[] { extension })!;
                }
            return contextOptions;
        }
    }
}
