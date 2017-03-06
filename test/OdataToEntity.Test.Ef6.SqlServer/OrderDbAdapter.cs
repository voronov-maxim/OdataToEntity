using OdataToEntity.Ef6;
using OdataToEntity.EfCore;
using OdataToEntity.Test.Ef6.SqlServer;
using System;

namespace OdataToEntity.Test
{
    public sealed class OrderDbDataAdapter : OeEfCoreDataAdapter<Model.OrderContext>
    {
        private String _databaseName;

        public OrderDbDataAdapter(String databaseName)
        {
            _databaseName = databaseName;
        }

        public override Object CreateDataContext()
        {
            return Model.OrderContext.Create(_databaseName);
        }
        public void ResetDatabase()
        {
            _databaseName = Model.OrderContext.GenerateDatabaseName();
        }
    }

    public sealed class OrderOeDataAdapter : OeEf6DataAdapter<OrderEf6Context>
    {
        private String _databaseName;

        public OrderOeDataAdapter(String databaseName)
        {
            _databaseName = databaseName;
        }

        public override Object CreateDataContext()
        {
            return new OrderEf6Context();
        }

        public new Db.OeQueryCache QueryCache => base.QueryCache;
    }
}
