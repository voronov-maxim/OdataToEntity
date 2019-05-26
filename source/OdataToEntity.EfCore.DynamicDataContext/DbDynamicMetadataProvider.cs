using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using OdataToEntity.EfCore.DynamicDataContext.InformationSchema;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using System.Linq;

namespace OdataToEntity.EfCore.DynamicDataContext
{
    public sealed class DbDynamicMetadataProvider : DynamicMetadataProvider, IDisposable
    {
        private readonly DbContextPool<SchemaContext> _dbContextPool;

        public DbDynamicMetadataProvider(DbContextOptions options)
        {
            _dbContextPool = new DbContextPool<SchemaContext>(options);
        }

        public void Dispose()
        {
            _dbContextPool.Dispose();
        }
        public override DynamicDependentPropertyInfo GetDependentProperties(String tableName, String navigationPropertyName)
        {
            throw new NotImplementedException();
        }
        public override String GetEntityName(String tableName)
        {
            throw new NotImplementedException();
        }
        public override IEnumerable<(String, String)> GetManyToManyProperties(String tableName)
        {
            return Array.Empty<(String, String)>();
        }
        public override IEnumerable<String> GetNavigationProperties(String tableName)
        {
            throw new NotImplementedException();
        }
        public override IEnumerable<String> GetPrimaryKey(String tableName)
        {
            throw new NotImplementedException();
        }
        public override IEnumerable<DynamicPropertyInfo> GetStructuralProperties(String tableName)
        {
            SchemaContext schemaContext = _dbContextPool.Rent();
            foreach (DbColumn column in schemaContext.GetColumns(tableName))
            {
                DatabaseGeneratedOption databaseGenerated;
                if (column.IsIdentity.GetValueOrDefault())
                    databaseGenerated = DatabaseGeneratedOption.Identity;
                else if (column.IsExpression.GetValueOrDefault())
                    databaseGenerated = DatabaseGeneratedOption.Computed;
                else
                    databaseGenerated = DatabaseGeneratedOption.None;
                yield return new DynamicPropertyInfo(column.ColumnName, column.DataType, databaseGenerated);
            }
            _dbContextPool.Return(schemaContext);
        }
        public override String GetTableName(String entityName)
        {
            return entityName;
        }
        public override IEnumerable<String> GetTableNames()
        {
            SchemaContext schemaContext = _dbContextPool.Rent();
            foreach (String tableName in schemaContext.Tables.Select(t => t.TableName))
                yield return tableName;
            _dbContextPool.Return(schemaContext);
        }
    }
}
