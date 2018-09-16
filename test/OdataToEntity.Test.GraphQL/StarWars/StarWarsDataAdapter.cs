using OdataToEntity.EfCore;
using System;

namespace OdataToEntity.Test.GraphQL.StarWars
{
    public sealed class StarWarsDataAdapter : OeEfCoreDataAdapter<StarWarsContext>
    {
        private readonly String _databaseName;

        public StarWarsDataAdapter(bool allowCache, String databaseName) :
            base(new Cache.OeQueryCache(allowCache))
        {
            _databaseName = databaseName;
        }

        public override object CreateDataContext()
        {
            return new StarWarsContext(_databaseName);
        }
    }
}
