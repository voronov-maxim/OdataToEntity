extern alias lq2db;
using OdataToEntity.Linq2Db;
using System;
using OdataToEntityDB = lq2db::OdataToEntity.Test.Model.OdataToEntityDB;

namespace OdataToEntity.Test
{
    internal sealed class OrderOeDataAdapter : OeLinq2DbDataAdapter<OdataToEntityDB>
    {
        public OrderOeDataAdapter(String databaseName)
        {
        }

        public override Object CreateDataContext()
        {
            LinqToDB.Common.Configuration.Linq.AllowMultipleQuery = true;

            return new OdataToEntityDB("OdataToEntity");
        }
    }
}
