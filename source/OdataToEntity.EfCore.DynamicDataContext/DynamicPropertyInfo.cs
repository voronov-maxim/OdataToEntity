using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace OdataToEntity.EfCore.DynamicDataContext
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
