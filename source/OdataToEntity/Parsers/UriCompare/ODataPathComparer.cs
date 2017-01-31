using Microsoft.OData.UriParser;
using System.Collections.Generic;
using System.Linq;

namespace OdataToEntity.Parsers.UriCompare
{
    internal sealed class ODataPathComparer : PathSegmentTranslator<bool>
    {
        private readonly IEnumerator<ODataPathSegment> _segments;

        private ODataPathComparer(ODataPath odataPath)
        {
            _segments = odataPath.GetEnumerator();
        }

        private ODataPathSegment GetNextSegment()
        {
            return _segments.MoveNext() ? _segments.Current : null;
        }
        public static bool Compare(ODataPath odataPath1, ODataPath odataPath2)
        {
            if (odataPath1.Count != odataPath2.Count)
                return false;

            ODataPathComparer comparer = null;
            try
            {
                comparer = new ODataPathComparer(odataPath1);
                foreach (bool result in odataPath2.WalkWith(comparer))
                    if (!result)
                        return false;
            }
            finally
            {
                if (comparer != null && comparer._segments != null)
                    comparer._segments.Dispose();
            }
            return true;
        }
        public override bool Translate(CountSegment segment)
        {
            return GetNextSegment() is CountSegment;
        }
        public override bool Translate(EntitySetSegment segment)
        {
            EntitySetSegment entitySetSegment = GetNextSegment() as EntitySetSegment;
            return entitySetSegment != null && entitySetSegment.EntitySet == segment.EntitySet;
        }
        public override bool Translate(KeySegment segment)
        {
            KeySegment keySegment = GetNextSegment() as KeySegment;
            return keySegment != null && keySegment.Keys.SequenceEqual(segment.Keys) && keySegment.EdmType == segment.EdmType && keySegment.NavigationSource == segment.NavigationSource;
        }
        public override bool Translate(NavigationPropertySegment segment)
        {
            NavigationPropertySegment navigationPropertySegment = GetNextSegment() as NavigationPropertySegment;
            return navigationPropertySegment != null && navigationPropertySegment.NavigationProperty == segment.NavigationProperty;
        }
        public override bool Translate(PropertySegment segment)
        {
            PropertySegment propertySegment = GetNextSegment() as PropertySegment;
            return propertySegment != null && propertySegment.Property == segment.Property;
        }
    }
}
