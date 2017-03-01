using Microsoft.EntityFrameworkCore;
using OdataToEntity.Test.Model;

namespace OdataToEntity.Test
{
    public class DbFixtureInitDb : DbFixture
    {
        private bool _initialized;

        public DbFixtureInitDb()
        {
        }
        public override void Initalize()
        {
            if (_initialized)
                return;

            _initialized = true;
            using (var context = new OrderContext())
                context.Database.ExecuteSqlCommand("dbo.ResetDb");
            base.ExecuteBatchAsync("Add").GetAwaiter().GetResult();
        }
    }
}
