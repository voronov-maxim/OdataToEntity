using System;
using System.Collections.Generic;

namespace OdataToEntity.EfCore.DynamicDataContext.InformationSchema
{
    public sealed class InformationSchemaMapping
    {
        public IReadOnlyList<OperationMapping> Operations { get; set; }
        public IReadOnlyList<TableMapping> Tables { get; set; }
    }

    public sealed class TableMapping
    {
        public TableMapping()
        {
        }
        public TableMapping(String dbName) : this(dbName, null)
        {
        }
        public TableMapping(String dbName, String edmName)
        {
            DbName = dbName;
            EdmName = edmName;
        }

        public String DbName { get; set; }
        public String EdmName { get; set; }
        public bool Exclude { get; set; }
        public IReadOnlyList<NavigationMapping> Navigations { get; set; }
    }

    public sealed class NavigationMapping
    {
        public NavigationMapping()
        {
        }
        public NavigationMapping(String constraintName, String navigationName)
        {
            ConstraintName = constraintName;
            NavigationName = navigationName;
        }

        public String ConstraintName { get; set; }
        public String NavigationName { get; set; }
        public String ManyToManyTarget { get; set; }
    }

    public sealed class OperationMapping
    {
        public OperationMapping()
        {
        }
        public OperationMapping(String dbName, String resultTableDbName)
        {
            DbName = dbName;
            ResultTableDbName = resultTableDbName;
        }

        public String DbName { get; set; }
        public bool Exclude { get; set; }
        public String ResultTableDbName { get; set; }
    }
}
