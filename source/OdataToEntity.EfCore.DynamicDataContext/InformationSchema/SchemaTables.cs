﻿using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations.Schema;

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

    [Table("PARAMETERS", Schema = "INFORMATION_SCHEMA")]
    public sealed class Parameter
    {
        [Column("SPECIFIC_SCHEMA")]
        public String SpecificSchema { get; set; }
        [Column("SPECIFIC_NAME")]
        public String SpecificName { get; set; }
        [Column("ORDINAL_POSITION")]
        public int OrdinalPosition { get; set; }
        [Column("PARAMETER_NAME")]
        public String ParameterName { get; set; }
        [Column("DATA_TYPE")]
        public String DataType { get; set; }
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
        [NotMapped]
        public String ReferencedTableName { get; set; }
    }

    [Table("ROUTINES", Schema = "INFORMATION_SCHEMA")]
    public sealed class Routine
    {
        [Column("SPECIFIC_SCHEMA")]
        public String SpecificSchema { get; set; }
        [Column("SPECIFIC_NAME")]
        public String SpecificName { get; set; }
        [Column("ROUTINE_SCHEMA")]
        public String RoutineSchema { get; set; }
        [Column("ROUTINE_NAME")]
        public String RoutineName { get; set; }
        [Column("DATA_TYPE")]
        public String DataType { get; set; }
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

        public DbSet<Column> Columns { get; set; }
        public DbSet<DbGeneratedColumn> DbGeneratedColumns { get; set; }
        public DbSet<KeyColumnUsage> KeyColumnUsage { get; set; }
        public DbSet<Parameter> Parameters { get; set; }
        public DbSet<ReferentialConstraint> ReferentialConstraints { get; set; }
        public DbSet<Routine> Routines { get; set; }
        public DbSet<TableConstraint> TableConstraints { get; set; }
        public DbSet<Table> Tables { get; set; }
    }
}