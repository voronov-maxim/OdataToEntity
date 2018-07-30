using System;
using System.Linq;
using System.Linq.Expressions;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace OdataToEntity.Test
{
    class Program
    {
        static void Main(string[] args)
        {
            new NC_PLNull(new NC_PLNull_DbFixtureInitDb()).ExpandExpandMany(0, false).GetAwaiter().GetResult();
        }
    }
}
