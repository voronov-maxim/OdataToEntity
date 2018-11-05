using System.Threading.Tasks;

namespace OdataToEntity.Test.GraphQL
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var tests = new StarWarsTests(new StarWarsFixture());
            await tests.can_query_for_the_id_and_friends_of_r2();
        }
    }
}
