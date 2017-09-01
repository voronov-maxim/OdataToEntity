using System;
using Microsoft.AspNetCore.Mvc;
using OdataToEntity.Test;
using OdataToEntity.Test.Model;

namespace OdataToEntity.AspServer
{
    public sealed class DbController : Controller
    {
        private readonly OrderOeDataAdapter _dataAdapter;

        public DbController(OrderOeDataAdapter dataAdapter)
        {
            _dataAdapter = dataAdapter;
        }

        public void Init()
        {
            OrderContext dbContext = null;
            try
            {
                dbContext = (OrderContext)_dataAdapter.CreateDataContext();
                dbContext.InitDb();
            }
            finally
            {
                if (dbContext != null)
                    _dataAdapter.CloseDataContext(dbContext);
            }
        }
        public void Reset()
        {
            _dataAdapter.ResetDatabase();
        }
    }
}
