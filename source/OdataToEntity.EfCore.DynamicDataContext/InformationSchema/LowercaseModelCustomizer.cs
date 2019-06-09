using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Linq.Expressions;
using System.Reflection;

namespace OdataToEntity.EfCore.DynamicDataContext.InformationSchema
{
    public sealed class LowercaseModelCustomizer : IModelCustomizer
    {
        private readonly ModelCustomizer _modelCustomizer;

        public LowercaseModelCustomizer(ModelCustomizerDependencies dependencies)
        {
            _modelCustomizer = new ModelCustomizer(dependencies);
        }

        public void Customize(Microsoft.EntityFrameworkCore.ModelBuilder modelBuilder, DbContext context)
        {
            _modelCustomizer.Customize(modelBuilder, context);

            foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
            {
                entityType.QueryFilter = GetFilter(entityType.ClrType);

                RelationalEntityTypeAnnotations relational = entityType.Relational();
                if (relational.Schema != null)
                    relational.Schema = relational.Schema.ToLowerInvariant();
                relational.TableName = entityType.Relational().TableName.ToLowerInvariant();

                foreach (IMutableProperty property in entityType.GetProperties())
                    property.Relational().ColumnName = property.Relational().ColumnName.ToLowerInvariant();
            }
        }
        private static LambdaExpression GetFilter(Type entityType)
        {
            PropertyInfo propertyInfo = entityType.GetProperty("TableSchema");
            if (propertyInfo == null)
                return null;

            ParameterExpression parameter = Expression.Parameter(entityType);
            MemberExpression property = Expression.Property(parameter, propertyInfo);

            BinaryExpression filter1 = Expression.NotEqual(property, Expression.Constant("pg_catalog"));
            BinaryExpression filter2 = Expression.NotEqual(property, Expression.Constant("information_schema"));
            BinaryExpression body = Expression.AndAlso(filter1, filter2);
            return Expression.Lambda(body, parameter);
        }
    }
}
