using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Query;
using OdataToEntity.EfCore.DynamicDataContext.ModelBuilder;
using System;
using System.Collections.Generic;

namespace OdataToEntity.EfCore.DynamicDataContext.InformationSchema
{
    public sealed class MySqlSchema : ProviderSpecificSchema
    {
        public MySqlSchema(DbContextOptions<DynamicDbContext> dynamicDbContextOptions)
            : base(dynamicDbContextOptions, CreatePool(dynamicDbContextOptions))
        {
            base.OperationAdapter = new OeMySqlEfCoreOperationAdapter(typeof(DynamicDbContext));
        }

        public override DynamicMetadataProvider CreateMetadataProvider(InformationSchemaSettings informationSchemaSettings)
        {
            return new DynamicMetadataProvider(this, informationSchemaSettings);
        }
        private static DbContextPool<SchemaContext> CreatePool(DbContextOptions<DynamicDbContext> dynamicDbContextOptions)
        {
            var optionsBuilder = new DbContextOptionsBuilder<SchemaContext>();
            optionsBuilder.ReplaceService<IModelCustomizer, MySqlModelCustomizer>();
            optionsBuilder.ReplaceService<IEntityMaterializerSource, MySqlModelCustomizer.MySqlEntityMaterializerSource>();

            var schemaOptions = (DbContextOptions<SchemaContext>)optionsBuilder.CreateOptions(dynamicDbContextOptions);
            return new DbContextPool<SchemaContext>(schemaOptions);
        }
        public override Type? GetColumnClrType(string dataType)
        {
            switch (dataType)
            {
                case "varchar":
                case "char":
                case "text":
                case "longtext":
                case "enum":
                case "set":
                    return typeof(String);
                case "int":
                    return typeof(int);
                case "bigint":
                    return typeof(long);
                case "tinyint":
                    return typeof(sbyte);
                case "datetime":
                    return typeof(DateTime);
                case "timestamp":
                    return typeof(DateTimeOffset);
                case "decimal":
                    return typeof(Decimal);
                case "":
                    return null;
                default:
                    throw new InvalidOperationException("Unknown data type " + dataType);
            }
        }
        public override IReadOnlyList<DbGeneratedColumn> GetDbGeneratedColumns()
        {
            var schemaContext = base.GetSchemaContext();
            try
            {
                var dbGeneratedColumns = new List<DbGeneratedColumn>();
                var dbSet = schemaContext.Set<MySqlModelCustomizer.MySqlDbGeneratedColumn>();
                foreach (MySqlModelCustomizer.MySqlDbGeneratedColumn column in
                    dbSet.FromSqlRaw("SELECT TABLE_SCHEMA, TABLE_NAME, COLUMN_NAME, EXTRA FROM INFORMATION_SCHEMA.COLUMNS WHERE EXTRA <> ''"))
                {
                    var dbGeneratedColumn = new DbGeneratedColumn()
                    {
                        ColumnName = column.ColumnName,
                        TableName = column.TableName,
                        TableSchema = column.TableSchema
                    };

                    if (column.Extra == "auto_increment")
                        dbGeneratedColumn.IsIdentity = true;
                    else
                        dbGeneratedColumn.IsComputed = true;

                    dbGeneratedColumns.Add(dbGeneratedColumn);
                }
                return dbGeneratedColumns;
            }
            finally
            {
                schemaContext.Dispose();
            }
        }
        public override String GetParameterName(String parameterName)
        {
            return parameterName;
        }
    }
}
