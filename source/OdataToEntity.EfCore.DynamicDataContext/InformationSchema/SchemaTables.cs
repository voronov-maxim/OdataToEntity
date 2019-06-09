using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Internal;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

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
        [Column("COLUMN_DEFAULT")]
        public String ColumnDefault { get; set; }
        [Column("IS_NULLABLE")]
        public String IsNullable { get; set; }
        [Column("DATA_TYPE")]
        public String DataType { get; set; }

        [NotMapped]
        public Type ClrType { get; set; }
        [NotMapped]
        public bool IsComputed { get; set; }
        [NotMapped]
        public bool IsIdentity { get; set; }
    }

    [Table("KEY_COLUMN_USAGE", Schema = "INFORMATION_SCHEMA")]
    public sealed class KeyColumnUsage
    {
        [Column("CONSTRAINT_SCHEMA")]
        public String ConstraintSchema { get; set; }
        [Column("CONSTRAINT_NAME")]
        public String ConstraintName { get; set; }
        [Column("TABLE_SCHEMA")]
        public String TableSchema { get; set; }
        [Column("TABLE_NAME")]
        public String TableName { get; set; }
        [Column("COLUMN_NAME")]
        public String ColumnName { get; set; }
        [Column("ORDINAL_POSITION")]
        public int OrdinalPosition { get; set; }
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

    [Table("TABLE_CONSTRAINTS", Schema = "INFORMATION_SCHEMA")]
    public sealed class TableConstraint
    {
        [Column("CONSTRAINT_SCHEMA")]
        public String ConstraintSchema { get; set; }
        [Column("CONSTRAINT_NAME")]
        public String ConstraintName { get; set; }
        [Column("TABLE_SCHEMA")]
        public String TableSchema { get; set; }
        [Column("TABLE_NAME")]
        public String TableName { get; set; }
        [Column("CONSTRAINT_TYPE")]
        public String ConstraintType { get; set; }
    }

    public sealed class DbGeneratedColumn
    {
        [Column("TABLE_SCHEMA")]
        public String TableSchema { get; set; }
        [Column("TABLE_NAME")]
        public String TableName { get; set; }
        [Column("COLUMN_NAME")]
        public String ColumnName { get; set; }
        [Column("is_identity")]
        public bool IsIdentity { get; set; }
        [Column("is_computed")]
        public bool IsComputed { get; set; }
    }

    public sealed class SchemaContext : DbContext
    {
        public SchemaContext(DbContextOptions options) : base(options)
        {
            base.ChangeTracker.AutoDetectChangesEnabled = false;
            base.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        }

        public DbQuery<Column> Columns { get; set; }
        public DbQuery<DbGeneratedColumn> DbGeneratedColumns { get; set; }
        public DbQuery<KeyColumnUsage> KeyColumnUsage { get; set; }
        public DbQuery<ReferentialConstraint> ReferentialConstraints { get; set; }
        public DbQuery<TableConstraint> TableConstraints { get; set; }
        public DbQuery<Table> Tables { get; set; }
    }
}
