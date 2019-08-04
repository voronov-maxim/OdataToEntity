using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Query;
using OdataToEntity.EfCore.DynamicDataContext.ModelBuilder;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OdataToEntity.EfCore.DynamicDataContext.InformationSchema
{
    public sealed class MySqlSchema : ProviderSpecificSchema
    {
        public MySqlSchema(DbContextOptions<DynamicDbContext> dynamicDbContextOptions)
            : base(dynamicDbContextOptions, CreatePool(dynamicDbContextOptions))
        {
            OperationAdapter = new OeMySqlEfCoreOperationAdapter(typeof(DynamicDbContext));
        }

        public override DynamicMetadataProvider CreateMetadataProvider(InformationSchemaMapping informationSchemaMapping)
        {
            return new DynamicMetadataProvider(this, informationSchemaMapping);
        }
        private static DbContextPool<SchemaContext> CreatePool(DbContextOptions<DynamicDbContext> dynamicDbContextOptions)
        {
            var optionsBuilder = new DbContextOptionsBuilder<SchemaContext>();
            optionsBuilder.ReplaceService<IModelCustomizer, MySqlModelCustomizer>();
            optionsBuilder.ReplaceService<IEntityMaterializerSource, MySqlModelCustomizer.MySqlEntityMaterializerSource>();

            DbContextOptions schemaOptions = optionsBuilder.Options;
            foreach (IDbContextOptionsExtension extension in dynamicDbContextOptions.Extensions)
                schemaOptions = schemaOptions.WithExtension(extension);
            return new DbContextPool<SchemaContext>(schemaOptions);
        }
        public override Type GetColumnClrType(string dataType)
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
            SchemaContext schemaContext = base.SchemaContextPool.Rent();
            try
            {
                var dbGeneratedColumns = new List<DbGeneratedColumn>();
                var dbSet = new InternalDbSet<MySqlModelCustomizer.MySqlDbGeneratedColumn>(schemaContext);
                foreach (MySqlModelCustomizer.MySqlDbGeneratedColumn column in dbSet.AsQueryable().Where(c => c.Extra != ""))
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
                base.SchemaContextPool.Return(schemaContext);
            }
        }
        public override String GetParameterName(String parameterName)
        {
            return parameterName;
        }

        public override bool IsCaseSensitive => false;
        public override OeEfCoreOperationAdapter OperationAdapter { get; }
    }
}
