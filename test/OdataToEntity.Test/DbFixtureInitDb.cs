using System;

namespace OdataToEntity.Test
{
    public class DbFixtureInitDb : DbFixture
    {
        private bool _initialized;

        public override void Initalize()
        {
            if (_initialized)
                return;

            _initialized = true;
            base.ExecuteBatchAsync("Add").GetAwaiter().GetResult();
        }
    }
}
