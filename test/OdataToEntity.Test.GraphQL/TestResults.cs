using GraphQL;
using GraphQL.Http;
using Newtonsoft.Json.Linq;
using System;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace OdataToEntity.Test.GraphQL
{
    public static class TestResults
    {
        public static readonly String can_query_for_droids = @"
{
    droid: [
      {
        name: 'C-3PO'
      }
    ]
}";
        public static readonly String can_query_for_friends_of_humans = @"
{
    human: [
      {
        name: 'Luke',
        friends: [
          {
            name: 'R2-D2',
            appearsIn: [
              {
                name: 'NEWHOPE'
              },
              {
                name: 'EMPIRE'
              },
              {
                name: 'JEDI'
              }
            ]
          },
          {
            name: 'C-3PO',
            appearsIn: [
              {
                name: 'NEWHOPE'
              },
              {
                name: 'EMPIRE'
              },
              {
                name: 'JEDI'
              }
            ]
          }
        ]
      }
    ]
}";
        public static readonly String can_query_for_humans = @"
{
    human: [
      {
        name: 'Luke',
        homePlanet: 'Tatooine'
      }
    ]
}";
        public static readonly String can_query_for_the_id_and_friends_of_r2 = @"
{
    hero: [
      {
        id: '1',
        name: 'Luke',
        friends: [
          {
            name: 'R2-D2'
          },
          {
            name: 'C-3PO'
          }
        ]
      },
      {
        id: '2',
        name: 'Vader',
        friends: null
      },
      {
        id: '3',
        name: 'R2-D2',
        friends: [
          {
            name: 'Luke'
          },
          {
            name: 'C-3PO'
          }
        ]
      },
      {
        id: '4',
        name: 'C-3PO',
        friends: null
      }
    ]
}";
        public static readonly String can_query_without_query_name = @"
{
    hero: [
      {
        name: 'Luke'
      },
      {
        name: 'Vader'
      },
      {
        name: 'R2-D2'
      },
      {
        name: 'C-3PO'
      }
    ]
}";
        public static readonly String create_generic_query_that_fetches_luke = @"
{
    human: [
        {
            name: 'Luke'
        }
    ]
}";
        public static readonly String identifies_r2_as_the_hero = @"
{
    hero: [
      {
        name: 'Luke'
      },
      {
        name: 'Vader'
      },
      {
        name: 'R2-D2'
      },
      {
        name: 'C-3PO'
      }
    ]
}";

        public static void Assert(String result, [CallerMemberName] String memberName = "")
        {
            FieldInfo fieldInfo = typeof(TestResults).GetField(memberName);
            var expected = (String)fieldInfo.GetValue(null);
            String formated = new DocumentWriter(true).Write(new ExecutionResult { Data = JObject.Parse(expected) });
            Xunit.Assert.Equal(formated, result);
        }
    }
}
