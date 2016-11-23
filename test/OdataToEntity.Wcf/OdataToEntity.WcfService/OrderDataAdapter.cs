using OdataToEntity.EfCore;
using System;

namespace OdataToEntity.Test
{
    public sealed class OrderDataAdapter : OeEfCoreDataAdapter<Model.OrderContext>
    {
        private String _databaseName;

        public OrderDataAdapter() : this(Model.OrderContext.GenerateDatabaseName())
        {
        }
        public OrderDataAdapter(String databaseName)
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
