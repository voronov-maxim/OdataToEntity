using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace OdataToEntity.EfCore.DynamicDataContext.InformationSchema
{
    [Table("COLUMNS", Schema = "INFORMATION_SCHEMA")]
    public sealed class Column
    {
        [Column("TABLE_SCHEMA")]
        public String TableSchema { get; set; } = null!;
        [Column("TABLE_NAME")]
        public String TableName { get; set; } = null!;
        [Column("COLUMN_NAME")]
        public String ColumnName { get; set; } = null!;
        [Column("ORDINAL_POSITION")]
        public int OrdinalPosition { get; set; }
        [Column("COLUMN_DEFAULT")]
        public String? ColumnDefault { get; set; }
        [Column("IS_NULLABLE")]
        public String? IsNullable { get; set; }
        [Column("DATA_TYPE")]
        public String DataType { get; set; } = null!;

        [NotMapped]
        public Type ClrType { get; set; } = null!;
        [NotMapped]
        public bool IsComputed { get; set; }
        [NotMapped]
        public bool IsIdentity { get; set; }
    }

    [Table("KEY_COLUMN_USAGE", Schema = "INFORMATION_SCHEMA")]
    public sealed class KeyColumnUsage
    {
        [Column("CONSTRAINT_SCHEMA")]
        public String ConstraintSchema { get; set; } = null!;
        [Column("CONSTRAINT_NAME")]
        public String ConstraintName { get; set; } = null!;
        [Column("TABLE_SCHEMA")]
        public String TableSchema { get; set; } = null!;
        [Column("TABLE_NAME")]
        public String TableName { get; set; } = null!;
        [Column("COLUMN_NAME")]
        public String ColumnName { get; set; } = null!;
        [Column("ORDINAL_POSITION")]
        public int OrdinalPosition { get; set; }
    }

    [Table("PARAMETERS", Schema = "INFORMATION_SCHEMA")]
    public sealed class Parameter
    {
        [Column("SPECIFIC_SCHEMA")]
        public String SpecificSchema { get; set; } = null!;
        [Column("SPECIFIC_NAME")]
        public String SpecificName { get; set; } = null!;
        [Column("ORDINAL_POSITION")]
        public int OrdinalPosition { get; set; }
        [Column("PARAMETER_NAME")]
        public String? ParameterName { get; set; }
        [Column("DATA_TYPE")]
        public String DataType { get; set; } = null!;
    }

    [Table("REFERENTIAL_CONSTRAINTS", Schema = "INFORMATION_SCHEMA")]
    public sealed class ReferentialConstraint
    {
        [Column("CONSTRAINT_SCHEMA")]
        public String ConstraintSchema { get; set; } = null!;
        [Column("CONSTRAINT_NAME")]
        public String ConstraintName { get; set; } = null!;
        [Column("UNIQUE_CONSTRAINT_SCHEMA")]
        public String UniqueConstraintSchema { get; set; } = null!;
        [Column("UNIQUE_CONSTRAINT_NAME")]
        public String UniqueConstraintName { get; set; } = null!;
        [NotMapped]
        public String ReferencedTableName { get; set; } = null!;
    }

    [Table("ROUTINES", Schema = "INFORMATION_SCHEMA")]
    public sealed class Routine
    {
        [Column("SPECIFIC_SCHEMA")]
        public String SpecificSchema { get; set; } = null!;
        [Column("SPECIFIC_NAME")]
        public String SpecificName { get; set; } = null!;
        [Column("ROUTINE_SCHEMA")]
        public String RoutineSchema { get; set; } = null!;
        [Column("ROUTINE_NAME")]
        public String RoutineName { get; set; } = null!;
        [Column("DATA_TYPE")]
        public String? DataType { get; set; }
    }

    [Table("TABLES", Schema = "INFORMATION_SCHEMA")]
    public sealed class Table
    {
        [Column("TABLE_SCHEMA")]
        public String TableSchema { get; set; } = null!;
        [Column("TABLE_NAME")]
        public String TableName { get; set; } = null!;
        [Column("TABLE_TYPE")]
        public String TableType { get; set; } = null!;
    }

    [Table("TABLE_CONSTRAINTS", Schema = "INFORMATION_SCHEMA")]
    public sealed class TableConstraint
    {
        [Column("CONSTRAINT_SCHEMA")]
        public String ConstraintSchema { get; set; } = null!;
        [Column("CONSTRAINT_NAME")]
        public String ConstraintName { get; set; } = null!;
        [Column("TABLE_SCHEMA")]
        public String? TableSchema { get; set; }
        [Column("TABLE_NAME")]
        public String? TableName { get; set; }
        [Column("CONSTRAINT_TYPE")]
        public String ConstraintType { get; set; } = null!;
    }

    public sealed class DbGeneratedColumn
    {
        [Column("TABLE_SCHEMA")]
        public String TableSchema { get; set; } = null!;
        [Column("TABLE_NAME")]
        public String TableName { get; set; } = null!;
        [Column("COLUMN_NAME")]
        public String ColumnName { get; set; } = null!;
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

        protected override void OnModelCreating(Microsoft.EntityFrameworkCore.ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Column>().HasNoKey();
            modelBuilder.Entity<DbGeneratedColumn>().HasNoKey();
            modelBuilder.Entity<KeyColumnUsage>().HasNoKey();
            modelBuilder.Entity<Parameter>().HasNoKey();
            modelBuilder.Entity<ReferentialConstraint>().HasNoKey();
            modelBuilder.Entity<Routine>().HasNoKey();
            modelBuilder.Entity<TableConstraint>().HasNoKey();
            modelBuilder.Entity<Table>().HasNoKey();
        }

        public DbSet<Column> Columns { get; set; } = null!;
        public DbSet<DbGeneratedColumn> DbGeneratedColumns { get; set; } = null!;
        public DbSet<KeyColumnUsage> KeyColumnUsage { get; set; } = null!;
        public DbSet<Parameter> Parameters { get; set; } = null!;
        public DbSet<ReferentialConstraint> ReferentialConstraints { get; set; } = null!;
        public DbSet<Routine> Routines { get; set; } = null!;
        public DbSet<TableConstraint> TableConstraints { get; set; } = null!;
        public DbSet<Table> Tables { get; set; } = null!;
    }
}
