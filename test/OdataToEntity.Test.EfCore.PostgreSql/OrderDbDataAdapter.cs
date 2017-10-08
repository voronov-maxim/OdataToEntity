using Microsoft.EntityFrameworkCore;
using OdataToEntity.EfCore;
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

    public sealed partial class OrderOeDataAdapter : OeEfCorePostgreSqlDataAdapter<Model.OrderContext>
    {
        private String _databaseName;

        public OrderOeDataAdapter(String databaseName) : base(Model.OrderContext.CreateOptions(), new Db.OeQueryCache())
        {
            _databaseName = databaseName;
        }

        public void ResetDatabase()
        {
            _databaseName = Model.OrderContext.GenerateDatabaseName();
        }

        public new Db.OeQueryCache QueryCache => base.QueryCache;
    }
}
