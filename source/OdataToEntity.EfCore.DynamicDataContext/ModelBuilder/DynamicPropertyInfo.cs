using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace OdataToEntity.EfCore.DynamicDataContext.ModelBuilder
{
    public readonly struct DynamicPropertyInfo
    {
        public String Name { get; }
        public Type Type { get; }
        public DatabaseGeneratedOption DatabaseGeneratedOption { get; }

        public DynamicPropertyInfo(String name, Type type, DatabaseGeneratedOption databaseGeneratedOption)
        {
            Name = name;
            Type = type;
            DatabaseGeneratedOption = databaseGeneratedOption;
        }
    }
}
