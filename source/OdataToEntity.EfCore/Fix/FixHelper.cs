using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace OdataToEntity.EfCore.Fix
{
    public static class FixHelper
    {
        public static DbContextOptions FixDistinctCount(this DbContextOptions options)
        {
            Type optionsBuilderType = typeof(DbContextOptionsBuilder<>).MakeGenericType(options.ContextType);
            var optionsBuilder = (DbContextOptionsBuilder?)Activator.CreateInstance(optionsBuilderType);
            if (optionsBuilder == null)
                throw new InvalidOperationException("Cannot create " + nameof(DbContextOptionsBuilder));

            RelationalOptionsExtension relationalOptionsExtension = options.Extensions.OfType<RelationalOptionsExtension>().Single();
            var serviceCollection = new ServiceCollection();
            relationalOptionsExtension.ApplyServices(serviceCollection);
            ServiceDescriptor serviceDescriptor = serviceCollection.Single(sd => sd.ServiceType == typeof(IRelationalSqlTranslatingExpressionVisitorFactory));

            Type fixFactoryType = typeof(FixRelationalSqlTranslatingExpressionVisitorFactory<>).MakeGenericType(serviceDescriptor.ImplementationType);
            Func<DbContextOptionsBuilder> func = optionsBuilder.ReplaceService<Object, Object>;
            MethodInfo replaceServiceMethod = func.Method.GetGenericMethodDefinition().MakeGenericMethod(typeof(IRelationalSqlTranslatingExpressionVisitorFactory), fixFactoryType);
            replaceServiceMethod.Invoke(optionsBuilder, null);

            DbContextOptions contextOptions = optionsBuilder.Options;
            foreach (IDbContextOptionsExtension extension in options.Extensions)
                if (extension is CoreOptionsExtension coreOptionsExtension)
                {
                    CoreOptionsExtension? newCoreOptions = null;
                    if (coreOptionsExtension.ReplacedServices != null)
                    {
                        newCoreOptions = contextOptions.GetExtension<CoreOptionsExtension>();
                        foreach (KeyValuePair<Type, Type> replacedService in coreOptionsExtension.ReplacedServices)
                            newCoreOptions = newCoreOptions.WithReplacedService(replacedService.Key, replacedService.Value);
                    }

                    if (coreOptionsExtension.LoggerFactory != null)
                    {
                        if (newCoreOptions == null)
                            newCoreOptions = contextOptions.GetExtension<CoreOptionsExtension>();
                        newCoreOptions = newCoreOptions.WithLoggerFactory(coreOptionsExtension.LoggerFactory);
                    }

                    if (newCoreOptions != null)
                        contextOptions = contextOptions.WithExtension(newCoreOptions);
                }
                else
                {
                    var withExtensionFunc = (Func<IDbContextOptionsExtension, DbContextOptions>)contextOptions.WithExtension<IDbContextOptionsExtension>;
                    var withExtension = withExtensionFunc.Method.GetGenericMethodDefinition().MakeGenericMethod(new[] { extension.GetType() });
                    contextOptions = (DbContextOptions?)withExtension.Invoke(contextOptions, new[] { extension }) ?? throw new InvalidOperationException("Cannot create " + nameof(DbContextOptions));
                }

            return contextOptions;
        }
    }
}
