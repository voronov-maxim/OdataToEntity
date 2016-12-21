using OdataToEntity.EfCore;
using System;

namespace OdataToEntity.Test
{
    public sealed class OrderDbDataAdapter : OeEfCoreDataAdapter<Model.OrderContext>
    {
        private String _databaseName;

        public OrderDbDataAdapter() : this(Model.OrderContext.GenerateDatabaseName())
        {
        }
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
}
