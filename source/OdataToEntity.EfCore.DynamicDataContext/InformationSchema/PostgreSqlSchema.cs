using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Internal;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OdataToEntity.EfCore.DynamicDataContext.InformationSchema
{
    public sealed class PostgreSqlSchema : ProviderSpecificSchema
    {
        private sealed class PostgreSqlDynamicOperationAdapter : OeEfCoreOperationAdapter
        {
            public PostgreSqlDynamicOperationAdapter() : base(typeof(Types.DynamicDbContext), true)
            {
            }

            public override IAsyncEnumerable<Object> ExecuteProcedureNonQuery(Object dataContext, String operationName, IReadOnlyList<KeyValuePair<String, Object>> parameters)
            {
                return base.ExecuteFunctionNonQuery(dataContext, operationName, parameters);
            }
            public override IAsyncEnumerable<Object> ExecuteProcedureReader(Object dataContext, String operationName, IReadOnlyList<KeyValuePair<String, Object>> parameters, Db.OeEntitySetAdapter entitySetAdapter)
            {
                return base.ExecuteFunctionReader(dataContext, operationName, parameters, entitySetAdapter);
            }
            public override IAsyncEnumerable<Object> ExecuteProcedurePrimitive(Object dataContext, String operationName, IReadOnlyList<KeyValuePair<String, Object>> parameters, Type returnType)
            {
                return base.ExecuteFunctionPrimitive(dataContext, operationName, parameters, returnType);
            }
        }

        public PostgreSqlSchema(DbContextOptions<Types.DynamicDbContext> dynamicDbContextOptions)
            : base(dynamicDbContextOptions, CreatePool(dynamicDbContextOptions))
        {
            base.IsDatabaseNullHighestValue = true;
            OperationAdapter = new PostgreSqlDynamicOperationAdapter();
        }

        private static DbContextPool<SchemaContext> CreatePool(DbContextOptions<Types.DynamicDbContext> dynamicDbContextOptions)
        {
            var optionsBuilder = new DbContextOptionsBuilder<SchemaContext>();
            optionsBuilder.ReplaceService<IModelCustomizer, PostgreSqlModelCustomizer>();
            DbContextOptions schemaOptions = optionsBuilder.Options;
            foreach (IDbContextOptionsExtension extension in dynamicDbContextOptions.Extensions)
                schemaOptions = schemaOptions.WithExtension(extension);
            return new DbContextPool<SchemaContext>(schemaOptions);
        }
        public override Type GetColumnClrType(String dataType)
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
                case "record":
                    return typeof(Object[]);
                case "void":
                    return typeof(void);
                case "USER-DEFINED":
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
                foreach (Column column in schemaContext.Columns.Where(c => c.ColumnDefault != null))
                {
                    var dbGeneratedColumn = new DbGeneratedColumn()
                    {
                        ColumnName = column.ColumnName,
                        TableName = column.TableName,
                        TableSchema = column.TableSchema
                    };

                    if (column.ColumnDefault.StartsWith("nextval("))
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

        public override bool IsCaseSensitive => true;
        public override OeEfCoreOperationAdapter OperationAdapter { get; }
    }
}
