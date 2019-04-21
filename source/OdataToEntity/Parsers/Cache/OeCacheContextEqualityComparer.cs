using OdataToEntity.Cache.UriCompare;
using System.Collections.Generic;

namespace OdataToEntity.Cache
{
    public sealed class OeCacheContextEqualityComparer : IEqualityComparer<OeCacheContext>
    {
        public bool Equals(OeCacheContext x, OeCacheContext y)
        {
            var comparer = new OeCacheComparer(x.ConstantToParameterMapper);
            if (comparer.Compare(x, y))
            {
                y.ParameterValues = comparer.ParameterValues;
                return true;
            }

            y.ParameterValues = null;
            return false;
        }
        public int GetHashCode(OeCacheContext obj)
        {
            return OeCacheComparer.GetCacheCode(obj);
        }
    }
}
