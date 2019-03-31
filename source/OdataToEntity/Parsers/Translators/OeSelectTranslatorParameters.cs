using System;
using System.Collections.Generic;
using System.Text;

namespace OdataToEntity.Parsers.Translators
{
    public struct OeSelectTranslatorParameters
    {
        public bool IsDatabaseNullHighestValue { get; set; }
        public OeMetadataLevel MetadataLevel { get; set; }
        public bool NavigationNextLink { get; set; }
        public OeSkipTokenNameValue[] SkipTokenNameValues { get; set; }
    }
}
