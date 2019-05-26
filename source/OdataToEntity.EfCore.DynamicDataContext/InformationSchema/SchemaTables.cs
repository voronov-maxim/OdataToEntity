using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Data.Common;
using System.Text;

namespace OdataToEntity.EfCore.DynamicDataContext.InformationSchema
{
    [Table("COLUMNS", Schema = "INFORMATION_SCHEMA")]
    public sealed class Column
    {
        [Column("TABLE_SCHEMA")]
        public String TableSchema { get; set; }
        [Column("TABLE_NAME")]
        public String TableName { get; set; }
        [Column("COLUMN_NAME")]
        public String ColumnName { get; set; }
        [Column("ORDINAL_POSITION")]
        public int OrdinalPosition { get; set; }
        [Column("IS_NULLABLE")]
        public String IsNullable { get; set; }
        [Column("DATA_TYPE")]
        public String DataType { get; set; }
    }

    [Table("CONSTRAINT_COLUMN_USAGE", Schema = "INFORMATION_SCHEMA")]
    public sealed class ConstraintColumnUsage
    {
        [Column("TABLE_SCHEMA")]
        public String TableSchema { get; set; }
        [Column("TABLE_NAME")]
        public String TableName { get; set; }
        [Column("COLUMN_NAME")]
        public String ColumnName { get; set; }
        [Column("CONSTRAINT_SCHEMA")]
        public String ConstraintSchema { get; set; }
        [Column("CONSTRAINT_NAME")]
        public String ConstraintName { get; set; }
    }

    [Table("REFERENTIAL_CONSTRAINTS", Schema = "INFORMATION_SCHEMA")]
    public sealed class ReferentialConstraint
    {
        [Column("CONSTRAINT_SCHEMA")]
        public String ConstraintSchema { get; set; }
        [Column("CONSTRAINT_NAME")]
        public String ConstraintName { get; set; }
        [Column("UNIQUE_CONSTRAINT_SCHEMA")]
        public String UniqueConstraintSchema { get; set; }
        [Column("UNIQUE_CONSTRAINT_NAME")]
        public String UniqueConstraintName { get; set; }
    }

    [Table("TABLES", Schema = "INFORMATION_SCHEMA")]
    public sealed class Table
    {
        [Column("TABLE_SCHEMA")]
        public String TableSchema { get; set; }
        [Column("TABLE_NAME")]
        public String TableName { get; set; }
        [Column("TABLE_TYPE")]
        public String TableType { get; set; }
    }

    public sealed class SchemaContext : DbContext
    {
        public SchemaContext(DbContextOptions options) : base(options)
        {
            base.ChangeTracker.AutoDetectChangesEnabled = false;
            base.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        }

        public ReadOnlyCollection<DbColumn> GetColumns(String tableName)
        {
            DbConnection connection = base.Database.GetDbConnection();
            using (DbCommand command = connection.CreateCommand())
            {
                connection.Open();
                try
                {
                    command.CommandText = "select * from " + tableName;
                    using (DbDataReader dataReader = command.ExecuteReader(CommandBehavior.SchemaOnly | CommandBehavior.KeyInfo))
                        return dataReader.GetColumnSchema();
                }
                finally
                {
                    connection.Close();
                }
            }
        }

        public DbQuery<Column> Columns { get; set; }
        public DbQuery<ConstraintColumnUsage> ConstraintColumnUsage { get; set; }
        public DbQuery<ReferentialConstraint> ReferentialConstraints { get; set; }
        public DbQuery<Table> Tables { get; set; }
    }
}
