using System.Threading.Tasks;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace OdataToEntity.Test.EfCore.PostgreSql
{
    class Program
    {
        static async Task Main()
        {
            await new RDBNull(new RDBNull_DbFixtureInitDb()).ExpandExpandSkipTop(0, false);
        }
    }
}
