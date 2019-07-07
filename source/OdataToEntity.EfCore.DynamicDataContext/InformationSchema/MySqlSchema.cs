using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using OdataToEntity.EfCore.DynamicDataContext.ModelBuilder;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OdataToEntity.EfCore.DynamicDataContext.InformationSchema
{
    public sealed class MySqlSchema : ProviderSpecificSchema
    {
        private sealed class MySqlDynamicOperationAdapter : OeEfCoreOperationAdapter
        {
            public MySqlDynamicOperationAdapter() : base(typeof(Types.DynamicDbContext))
            {
            }

            protected override String GetProcedureName(Object dataContext, String operationName, IReadOnlyList<KeyValuePair<String, Object>> parameters)
            {
                var sql = new StringBuilder("call ");
                sql.Append(operationName);
                if (parameters.Count > 0)
                {
                    sql.Append('(');
                    String[] parameterNames = GetParameterNames(dataContext, parameters);
                    sql.Append(String.Join(",", parameterNames));
                    sql.Append(')');
                }
                return sql.ToString();
            }
        }

        public MySqlSchema(DbContextOptions<Types.DynamicDbContext> dynamicDbContextOptions)
            : base(dynamicDbContextOptions, CreatePool(dynamicDbContextOptions))
        {
            OperationAdapter = new MySqlDynamicOperationAdapter();
        }

        public override DynamicMetadataProvider CreateMetadataProvider(InformationSchemaMapping informationSchemaMapping)
        {
            return new DynamicMetadataProvider(this, informationSchemaMapping);
        }
        private static DbContextPool<SchemaContext> CreatePool(DbContextOptions<Types.DynamicDbContext> dynamicDbContextOptions)
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
                var dbSet = new InternalDbQuery<MySqlModelCustomizer.MySqlDbGeneratedColumn>(schemaContext);
                foreach (MySqlModelCustomizer.MySqlDbGeneratedColumn column in dbSet.Where(c => c.Extra != ""))
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
