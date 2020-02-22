using GraphQL;
using System;
using System.Collections.Generic;
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
            String query = @"
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
            String query = @"
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
            String query = @"
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
            String query = @"
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
            String query = @"
               {
                  hero {
                    id
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
            String query = @"
                query humanQuery($id: ID) {
                  human(id: $id) {
                    name
                  }
                }
            ";

            var inputs = new Inputs( new Dictionary<String, Object>() { { "id", "1" } });
            String result = await Fixture.Execute(query, inputs);
            TestResults.Assert(result);
        }
        [Fact]
        public async Task filter_not_required_single_navigation_property()
        {
            String query = @"
               {
                  hero {
                    id
                    name
                    voice(birthday: ""1951-09-25"") {
                        name
                        birthday
                    }
                  }
               }
            ";

            String result = await Fixture.Execute(query);
            TestResults.Assert(result);
        }
        [Fact]
        public async Task filter_required_single_navigation_property()
        {
            String query = @"
               {
                  hero {
                    name
                    actor(birthday: ""1951-09-25"") {
                        name
                        birthday
                    }
                  }
               }
            ";

            String result = await Fixture.Execute(query);
            TestResults.Assert(result);
        }
        [Fact]
        public async Task identifies_r2_as_the_hero()
        {
            String query = @"
                query HeroNameQuery {
                  hero {
                    id
                    name
                  }
                }
            ";

            String result = await Fixture.Execute(query);
            TestResults.Assert(result);
        }

        [Fact]
        public async Task referenced_models()
        {
            await can_query_for_droids();

            String query = @"
               {
                  orders2 {
                    name
                    customer {
                        name
                    }
                  }
               }
            ";

            String result = await Fixture.Execute(query);
            TestResults.Assert(result);
        }

        private StarWarsFixture Fixture { get; }
    }
}
