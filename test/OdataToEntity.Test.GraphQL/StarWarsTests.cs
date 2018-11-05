using GraphQL;
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Xunit;

namespace OdataToEntity.Test.GraphQL
{
    public sealed class StarWarsTests : IClassFixture<StarWarsFixture>
    {
        public StarWarsTests(StarWarsFixture fixture)
        {
            Fixture = fixture;
        }

        [Fact]
        public async Task can_query_for_droids()
        {
            var query = @"
               {
                  droid(id: ""4"") {
                    name
                  }
               }
            ";

            String result = await Fixture.Execute(query);
            TestResults.Assert(result);
        }
        [Fact]
        public async Task can_query_for_friends_of_humans()
        {
            var query = @"
               {
                  human(id: ""1"") {
                    name
                    friends {
                      name
                      appearsIn {
                        name
                      }
                    }
                  }
               }
            ";

            String result = await Fixture.Execute(query);
            TestResults.Assert(result);
        }
        [Fact]
        public async Task can_query_for_humans()
        {
            var query = @"
               {
                  human(id: ""1"") {
                    name
                    homePlanet
                  }
               }
            ";

            String result = await Fixture.Execute(query);
            TestResults.Assert(result);
        }
        [Fact]
        public async Task can_query_for_the_id_and_friends_of_r2()
        {
            var query = @"
                query HeroNameAndFriendsQuery {
                  hero {
                    id
                    name
                    friends {
                      name
                    }
                  }
                }
            ";

            String result = await Fixture.Execute(query);
            TestResults.Assert(result);
        }
        [Fact]
        public async Task can_query_without_query_name()
        {
            var query = @"
               {
                  hero {
                    name
                  }
               }
            ";

            String result = await Fixture.Execute(query);
            TestResults.Assert(result);
        }
        [Fact]
        public async Task create_generic_query_that_fetches_luke()
        {
            var query = @"
                query humanQuery($id: String!) {
                  human(id: $id) {
                    name
                  }
                }
            ";

            var inputs = new Inputs { { "id", "1" } };
            String result = await Fixture.Execute(query, inputs);
            TestResults.Assert(result);
        }
        [Fact]
        public async Task identifies_r2_as_the_hero()
        {
            var query = @"
                query HeroNameQuery {
                  hero {
                    name
                  }
                }
            ";

            String result = await Fixture.Execute(query);
            TestResults.Assert(result);
        }

        private StarWarsFixture Fixture { get; }
    }
}
