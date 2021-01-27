using System.Threading.Tasks;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace OdataToEntity.Test.InMemory
{
    class Program
    {
        static async Task Main()
        {
            await new RDBNull(new RDBNull_DbFixtureInitDb()).FilterStringLe(0);
        }
    }
}
