using OdataToEntity.Cache.UriCompare;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace OdataToEntity.Cache
{
    public sealed class OeCacheContextEqualityComparer : IEqualityComparer<OeCacheContext>
    {
        public bool Equals([AllowNull] OeCacheContext x, [AllowNull] OeCacheContext y)
        {
            if (Object.ReferenceEquals(x, y))
                return true;

            if (x == null || y == null)
                return false;

            var comparer = new OeCacheComparer(x.ConstantToParameterMapper);
            if (comparer.Compare(x, y))
            {
                y.ParameterValues = comparer.ParameterValues;
                return true;
            }

            return false;
        }
        public int GetHashCode(OeCacheContext obj)
        {
            return OeCacheComparer.GetCacheCode(obj);
        }
    }
}
