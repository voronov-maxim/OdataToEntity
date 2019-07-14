using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace OdataToEntity.EfCore.DynamicDataContext.ModelBuilder
{
    public readonly struct DynamicPropertyInfo
    {
        public DynamicPropertyInfo(String name, Type type, bool isNullable, DatabaseGeneratedOption databaseGeneratedOption)
        {
            IsNullable = isNullable;
            Name = name;
            Type = type;
            DatabaseGeneratedOption = databaseGeneratedOption;
        }

        public bool IsNullable { get; }
        public String Name { get; }
        public Type Type { get; }
        public DatabaseGeneratedOption DatabaseGeneratedOption { get; }
    }
}
