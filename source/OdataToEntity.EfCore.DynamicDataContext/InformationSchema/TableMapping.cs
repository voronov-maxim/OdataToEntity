using System;
using System.Collections.Generic;

namespace OdataToEntity.EfCore.DynamicDataContext.InformationSchema
{
    public sealed class TableMapping
    {
        public TableMapping()
        {
        }
        public TableMapping(String dbName)
        {
            DbName = dbName;
        }

        public String DbName { get; set; }
        public String EdmName { get; set; }
        public bool Exclude { get; set; }
        public ICollection<NavigationMapping> Navigations { get; set; }
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
}
