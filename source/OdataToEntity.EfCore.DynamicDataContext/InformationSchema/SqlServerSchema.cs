using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Internal;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OdataToEntity.EfCore.DynamicDataContext.InformationSchema
{
    public sealed class SqlServerSchema : ProviderSpecificSchema
    {
        public SqlServerSchema(DbContextOptions<DynamicDbContext> dynamicDbContextOptions)
            : base(dynamicDbContextOptions, CreatePool(dynamicDbContextOptions))
        {
            base.OperationAdapter = new OeEfCoreOperationAdapter(typeof(DynamicDbContext));
        }

        private static DbContextPool<SchemaContext> CreatePool(DbContextOptions<DynamicDbContext> dynamicDbContextOptions)
        {
            var optionsBuilder = new DbContextOptionsBuilder<SchemaContext>();
            optionsBuilder.ReplaceService<IModelCustomizer, SqlServerModelCustomizer>();

            var schemaOptions = (DbContextOptions<SchemaContext>)optionsBuilder.CreateOptions(dynamicDbContextOptions);
            return new DbContextPool<SchemaContext>(schemaOptions);
        }
        public override Type? GetColumnClrType(String dataType)
        {
            switch (dataType)
            {
                case "bigint":
                    return typeof(long);
                case "binary":
                    return typeof(byte[]);
                case "bit":
                    return typeof(bool);
                case "char":
                    return typeof(String);
                case "date":
                case "datetime":
                case "datetime2":
                    return typeof(DateTime);
                case "datetimeoffset":
                    return typeof(DateTimeOffset);
                case "decimal":
                    return typeof(Decimal);
                case "float":
                    return typeof(double);
                case "image":
                    return typeof(byte[]);
                case "int":
                    return typeof(int);
                case "money":
                    return typeof(Decimal);
                case "nchar":
                case "ntext":
                    return typeof(String);
                case "numeric":
                    return typeof(Decimal);
                case "nvarchar":
                    return typeof(String);
                case "real":
                    return typeof(float);
                case "rowversion":
                    return typeof(byte[]);
                case "smalldatetime":
                    return typeof(DateTime);
                case "smallint":
                    return typeof(short);
                case "smallmoney":
                    return typeof(Decimal);
                case "sql_variant":
                    return typeof(Object);
                case "text":
                    return typeof(String);
                case "time":
                    return typeof(TimeSpan);
                case "timestamp":
                    return typeof(byte[]);
                case "tinyint":
                    return typeof(byte);
                case "uniqueidentifier":
                    return typeof(Guid);
                case "varbinary":
                case "varchar":
                case "xml":
                    return typeof(String);
                case "TABLE":
                case "table type":
                    return null;
                default:
                    throw new InvalidOperationException("Unknown data type " + dataType);
            }
        }
        public override IReadOnlyList<DbGeneratedColumn> GetDbGeneratedColumns()
        {
            SchemaContext schemaContext = base.GetSchemaContext();
            try
            {
                return schemaContext.DbGeneratedColumns.FromSqlRaw(
                    "select OBJECT_SCHEMA_NAME(object_id) TABLE_SCHEMA, OBJECT_NAME(object_id) TABLE_NAME, name COLUMN_NAME, is_identity, is_computed from sys.columns where is_identity = 1 or is_computed = 1;")
                    .ToList();
            }
            finally
            {
                schemaContext.Dispose();
            }
        }
        public override String GetParameterName(String parameterName)
        {
            return parameterName[0] == '@' ? parameterName.Substring(1) : parameterName;
        }
    }
}
