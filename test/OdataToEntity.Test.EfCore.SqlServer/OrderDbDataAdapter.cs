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

    public sealed class OrderOeDataAdapter : OeEfCoreDataAdapter<Model.OrderContext>
    {
        private String _databaseName;

        public OrderOeDataAdapter(String databaseName) : base(CreateOptions(), new Db.OeQueryCache())
        {
            _databaseName = databaseName;
        }

        internal static DbContextOptions CreateOptions()
        {
            var optionsBuilder = new DbContextOptionsBuilder<Model.OrderContext>();
            optionsBuilder.UseSqlServer(@"Server=.\sqlexpress;Initial Catalog=OdataToEntity;Trusted_Connection=Yes;");
            return optionsBuilder.Options;
        }
        public void ResetDatabase()
        {
            _databaseName = Model.OrderContext.GenerateDatabaseName();
        }

        public new Db.OeQueryCache QueryCache => base.QueryCache;
    }
}
