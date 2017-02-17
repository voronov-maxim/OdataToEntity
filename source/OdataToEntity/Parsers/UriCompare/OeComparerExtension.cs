using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OdataToEntity.Parsers.UriCompare
{
    public static class OeComparerExtension
    {
        public static bool IsEqual(this IEdmTypeReference @this, IEdmTypeReference edmTypeReference)
        {
            if (@this == edmTypeReference)
                return true;
            if (@this == null || edmTypeReference == null)
                return false;

            return @this.Definition == edmTypeReference.Definition && @this.IsNullable == edmTypeReference.IsNullable;
        }
    }
}
