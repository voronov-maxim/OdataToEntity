using System.Collections.Generic;

namespace OdataToEntity.Parsers.UriCompare
{
    public sealed class OeParseUriContextEqualityComparer : IEqualityComparer<OeParseUriContext>
    {
        public bool Equals(OeParseUriContext x, OeParseUriContext y)
        {
            var comparer = new OeODataUriComparer(x.ConstantToParameterMapper);
            if (comparer.Compare(x, y))
            {
                y.ParameterValues = comparer.ParameterValues;
                return true;
            }

            y.ParameterValues = null;
            return false;
        }
        public int GetHashCode(OeParseUriContext obj)
        {
            return OeODataUriComparer.GetCacheCode(obj);
        }
    }
}
