using OdataToEntity.Test.GraphQL.StarWars;
using System;
using System.Collections.Generic;
using System.Text;

namespace OdataToEntity.Test.Model
{
    public sealed class Order2DataAdapter : EfCore.OeEfCoreDataAdapter<Order2Context>
    {
        public Order2DataAdapter(bool allowCache, String databaseName) :
            base(StarWarsContext.Create<Order2Context>(databaseName), new Cache.OeQueryCache(allowCache))
        {
        }
    }
}
