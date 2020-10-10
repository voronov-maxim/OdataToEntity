using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Linq.Expressions;
using System.Reflection;

namespace OdataToEntity.EfCore.DynamicDataContext.InformationSchema
{
    public sealed class PostgreSqlModelCustomizer : IModelCustomizer
    {
        private readonly ModelCustomizer _modelCustomizer;

        public PostgreSqlModelCustomizer(ModelCustomizerDependencies dependencies)
        {
            _modelCustomizer = new ModelCustomizer(dependencies);
        }

        public void Customize(Microsoft.EntityFrameworkCore.ModelBuilder modelBuilder, DbContext context)
        {
            _modelCustomizer.Customize(modelBuilder, context);

            foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
            {
                entityType.SetQueryFilter(GetFilter(entityType.ClrType));

                if (entityType.GetSchema() != null)
                    entityType.SetSchema(entityType.GetSchema().ToLowerInvariant());
                entityType.SetTableName(entityType.GetTableName().ToLowerInvariant());

                foreach (IMutableProperty property in entityType.GetProperties())
                {
                    var storeObjectIdentifier = StoreObjectIdentifier.Table(entityType.GetTableName(), entityType.GetSchema());
                    property.SetColumnName(property.GetColumnName(storeObjectIdentifier).ToLowerInvariant());
                }
            }

            Expression<Func<Parameter, bool>> parameterFilter = t => t.SpecificSchema != "pg_catalog" && t.SpecificSchema != "information_schema";
            modelBuilder.Model.FindEntityType(typeof(Parameter)).SetQueryFilter(parameterFilter);

            Expression<Func<Routine, bool>> routineFilter = t => t.SpecificSchema != "pg_catalog" && t.SpecificSchema != "information_schema" && t.DataType != "internal";
            modelBuilder.Model.FindEntityType(typeof(Routine)).SetQueryFilter(routineFilter);
        }
        private static LambdaExpression GetFilter(Type entityType)
        {
            PropertyInfo? propertyInfo = entityType.GetProperty("TableSchema");
            if (propertyInfo == null)
            {
                propertyInfo = entityType.GetProperty("ConstraintSchema");
                if (propertyInfo == null)
                {
                    propertyInfo = entityType.GetProperty("SpecificSchema");
                    if (propertyInfo == null)
                        throw new InvalidOperationException("Unknow PostgreSql schema");
                }
            }

            ParameterExpression parameter = Expression.Parameter(entityType);
            MemberExpression property = Expression.Property(parameter, propertyInfo);

            BinaryExpression filter1 = Expression.NotEqual(property, Expression.Constant("pg_catalog"));
            BinaryExpression filter2 = Expression.NotEqual(property, Expression.Constant("information_schema"));
            BinaryExpression body = Expression.AndAlso(filter1, filter2);
            return Expression.Lambda(body, parameter);
        }
    }
}
