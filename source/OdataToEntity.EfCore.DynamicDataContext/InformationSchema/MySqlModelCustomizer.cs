using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Query.Internal;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;

namespace OdataToEntity.EfCore.DynamicDataContext.InformationSchema
{
    public sealed class MySqlModelCustomizer : IModelCustomizer
    {
        internal sealed class MySqlEntityMaterializerSource : EntityMaterializerSource
        {
            public MySqlEntityMaterializerSource(EntityMaterializerSourceDependencies dependencies) : base(dependencies)
            {
            }

            public override Expression CreateMaterializeExpression(IEntityType entityType, String entityInstanceName, Expression materializationContextExpression)
            {
                var block = (BlockExpression)base.CreateMaterializeExpression(entityType, entityInstanceName, materializationContextExpression);
                var expressions = new List<Expression>(block.Expressions);

                expressions[expressions.Count - 1] = Expression.Call(((Action<Object>)Initialize).Method, block.Variables);
                expressions.Add(block.Expressions[block.Expressions.Count - 1]);
                return Expression.Block(block.Variables, expressions);
            }
            private static void Initialize(Object entity)
            {
                if (entity is KeyColumnUsage keyColumnUsage && keyColumnUsage.ConstraintName == "PRIMARY")
                    keyColumnUsage.ConstraintName = "PK_" + keyColumnUsage.TableName;
                else if (entity is TableConstraint tableConstraint && tableConstraint.ConstraintName == "PRIMARY")
                    tableConstraint.ConstraintName = "PK_" + tableConstraint.TableName;
                else if (entity is ReferentialConstraint referentialConstraint && referentialConstraint.UniqueConstraintName == "PRIMARY")
                    referentialConstraint.UniqueConstraintName = "PK_" + referentialConstraint.ReferencedTableName;
                else if (entity is Routine routine && routine.DataType == "")
                    routine.DataType = null;
            }
        }

        internal sealed class MySqlDbGeneratedColumn
        {
            [Column("TABLE_SCHEMA")]
            public String TableSchema { get; set; } = null!;
            [Column("TABLE_NAME")]
            public String TableName { get; set; } = null!;
            [Column("COLUMN_NAME")]
            public String ColumnName { get; set; } = null!;
            [Column("EXTRA")]
            public String Extra { get; set; } = null!;
        }

        private readonly ModelCustomizer _modelCustomizer;

        public MySqlModelCustomizer(ModelCustomizerDependencies dependencies)
        {
            _modelCustomizer = new ModelCustomizer(dependencies);
        }

        public void Customize(Microsoft.EntityFrameworkCore.ModelBuilder modelBuilder, DbContext context)
        {
            _modelCustomizer.Customize(modelBuilder, context);

            GetDatabaseName(context);
            String databaseName = GetDatabaseName(context);
            foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
                entityType.SetQueryFilter(GetFilter(entityType.ClrType, databaseName));

            IMutableProperty specificSchema = (Property)modelBuilder.Model.FindEntityType(typeof(Routine)).FindProperty(nameof(Routine.SpecificSchema));
            specificSchema.SetColumnName("ROUTINE_SCHEMA");

            IMutableEntityType referentialConstraint = modelBuilder.Model.FindEntityType(typeof(ReferentialConstraint));
            referentialConstraint.AddProperty(nameof(ReferentialConstraint.ReferencedTableName), typeof(String)).SetColumnName("REFERENCED_TABLE_NAME");

            Expression<Func<Parameter, bool>> parameterFilter = t => t.SpecificSchema == databaseName && t.OrdinalPosition > 0;
            modelBuilder.Model.FindEntityType(typeof(Parameter)).SetQueryFilter(parameterFilter);

            modelBuilder.Entity<MySqlDbGeneratedColumn>().HasNoKey();
        }
        private static String GetDatabaseName(DbContext context)
        {
            var serviceProvider = (IInfrastructure<IServiceProvider>)context;
            IDbContextOptions options = serviceProvider.GetService<IDbContextServices>().ContextOptions;
            foreach (IDbContextOptionsExtension extension in options.Extensions)
                if (extension is RelationalOptionsExtension relationalOptionsExtension)
                {
                    var builder = new DbConnectionStringBuilder() { ConnectionString = relationalOptionsExtension.ConnectionString };
                    return (String)builder["database"];
                }

            throw new InvalidOperationException("Not connection string found in DbContext");
        }
        private static LambdaExpression GetFilter(Type entityType, String databaseName)
        {
            PropertyInfo? propertyInfo = entityType.GetProperty("TableSchema");
            if (propertyInfo == null)
            {
                propertyInfo = entityType.GetProperty("ConstraintSchema");
                if (propertyInfo == null)
                {
                    propertyInfo = entityType.GetProperty("SpecificSchema");
                    if (propertyInfo == null)
                        throw new InvalidOperationException("Unknow MySql schema");
                }
            }

            ParameterExpression parameter = Expression.Parameter(entityType);
            MemberExpression property = Expression.Property(parameter, propertyInfo);

            BinaryExpression filter = Expression.Equal(property, Expression.Constant(databaseName));
            return Expression.Lambda(filter, parameter);
        }
    }
}
