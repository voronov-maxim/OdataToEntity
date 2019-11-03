using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Reflection;

namespace OdataToEntity.EfCore.Fix
{
    public static class FixHelper
    {
        public static DbContextOptions FixDistinctCount(this DbContextOptions options)
        {
            Type optionsBuilderType = typeof(DbContextOptionsBuilder<>).MakeGenericType(options.ContextType);
            var optionsBuilder = (DbContextOptionsBuilder)Activator.CreateInstance(optionsBuilderType);

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
                if (extension.GetType() != typeof(CoreOptionsExtension))
                    contextOptions = contextOptions.WithExtension(extension);

            return contextOptions;
        }
    }
}
