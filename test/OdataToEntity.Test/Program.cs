using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace OdataToEntity.Test
{
    class Program
    {
        static async Task Main()
        {
            await new PLNull(new PLNull_DbFixtureInitDb()).ExpandExpandMany(1, false);
        }
    }
}
