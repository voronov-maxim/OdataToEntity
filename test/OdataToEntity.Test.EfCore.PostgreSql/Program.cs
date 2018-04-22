using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace OdataToEntity.Test.EfCore.PostgreSql
{
    class Program
    {
        static void Main(string[] args)
        {
            new AC_RDBNull(new AC_RDBNull_DbFixtureInitDb()).Table(0).GetAwaiter().GetResult();
        }
    }
}
