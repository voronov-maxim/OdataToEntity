using System;
using Microsoft.AspNetCore.Mvc;
using OdataToEntity.Test;
using OdataToEntity.Test.Model;

namespace OdataToEntityCore.Asp
{
    public sealed class DbController : Controller
    {
        private readonly OrderDataAdapter _dataAdapter;

        public DbController(OrderDataAdapter dataAdapter)
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
