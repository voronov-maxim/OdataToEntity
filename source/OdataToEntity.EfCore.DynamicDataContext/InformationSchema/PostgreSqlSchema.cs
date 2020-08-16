using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Internal;
using OdataToEntity.EfCore.Postgresql;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OdataToEntity.EfCore.DynamicDataContext.InformationSchema
{
    public sealed class PostgreSqlSchema : ProviderSpecificSchema
    {
        public PostgreSqlSchema(DbContextOptions<DynamicDbContext> dynamicDbContextOptions)
            : base(dynamicDbContextOptions, CreatePool(dynamicDbContextOptions))
        {
            base.ExpressionVisitor = new OeDateTimeOffsetMembersVisitor();
            base.IsDatabaseNullHighestValue = true;
            base.IsCaseSensitive = true;
            base.OperationAdapter = new OePostgreSqlEfCoreOperationAdapter(typeof(DynamicDbContext));
        }

        private static DbContextPool<SchemaContext> CreatePool(DbContextOptions<DynamicDbContext> dynamicDbContextOptions)
        {
            var optionsBuilder = new DbContextOptionsBuilder<SchemaContext>();
            optionsBuilder.ReplaceService<IModelCustomizer, PostgreSqlModelCustomizer>();

            var schemaOptions = (DbContextOptions<SchemaContext>)optionsBuilder.CreateOptions(dynamicDbContextOptions);
            return new DbContextPool<SchemaContext>(schemaOptions);
        }
        public override Type? GetColumnClrType(String dataType)
        {
            switch (dataType)
            {
                case "smallint":
                    return typeof(short);
                case "integer":
                    return typeof(int);
                case "bigint":
                    return typeof(long);
                case "real":
                    return typeof(float);
                case "double precision":
                    return typeof(double);
                case "numeric":
                case "money":
                    return typeof(Decimal);
                case "text":
                case "character varying":
                case "character":
                case "citext":
                case "json":
                case "jsonb":
                case "xml":
                    return typeof(String);
                case "bit(1)":
                case "boolean":
                    return typeof(bool);
                case "bit(n)":
                case "bit varying":
                    return typeof(System.Collections.BitArray);
                case "hstore":
                    return typeof(IDictionary<String, String>);
                case "uuid":
                    return typeof(Guid);
                case "cidr":
                    return typeof(ValueTuple<System.Net.IPAddress, int>);
                case "inet":
                    return typeof(System.Net.IPAddress);
                case "macaddr":
                    return typeof(System.Net.NetworkInformation.PhysicalAddress);
                case "date":
                case "timestamp without time zone":
                    return typeof(DateTime);
                case "interval":
                    return typeof(TimeSpan);
                case "timestamp":
                case "timestamp with time zone":
                    return typeof(DateTimeOffset);
                case "time":
                    return typeof(TimeSpan);
                case "time with time zone":
                    return typeof(DateTimeOffset);
                case "bytea":
                case "cstring":
                    return typeof(byte[]);
                case "oid":
                case "xid":
                case "cid":
                    return typeof(uint);
                case "oidvector":
                    return typeof(uint[]);
                case "name":
                    return typeof(String);
                case "(internal) char":
                    return typeof(char);
                case "void":
                    return typeof(void);
                case "record": //typeof(Object[]);
                case "USER-DEFINED":
                case "ARRAY":
                case "tid":
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
                var dbGeneratedColumns = new List<DbGeneratedColumn>();
                foreach (Column column in schemaContext.Columns.AsQueryable().Where(c => c.ColumnDefault != null))
                {
                    var dbGeneratedColumn = new DbGeneratedColumn()
                    {
                        ColumnName = column.ColumnName,
                        TableName = column.TableName,
                        TableSchema = column.TableSchema
                    };

                    if (column.ColumnDefault!.StartsWith("nextval("))
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
