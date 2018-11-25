extern alias lq2db;
using OdataToEntity.Linq2Db;
using System;
using OdataToEntityDB = lq2db::OdataToEntity.Test.Model.OdataToEntityDB;

namespace OdataToEntity.Test.Model
{
    public sealed class OrderOeDataAdapter : OeLinq2DbDataAdapter<OdataToEntityDB>
    {
        public OrderOeDataAdapter(bool allowCache, bool useRelationalNulls, String databaseName) : base(new Cache.OeQueryCache(allowCache))
        {
        }

        public override Object CreateDataContext()
        {
            LinqToDB.Common.Configuration.Linq.AllowMultipleQuery = true;
            return new OdataToEntityDB();
        }
        public ModelBuilder.OeEdmModelMetadataProvider CreateMetadataProvider()
        {
            return new OeLinq2DbEdmModelMetadataProvider();
        }

        public new Cache.OeQueryCache QueryCache => base.QueryCache;
    }
}
