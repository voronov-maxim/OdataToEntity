namespace OdataToEntity.Parsers.Translators
{
    public struct OeSelectTranslatorParameters
    {
        public bool IsDatabaseNullHighestValue { get; set; }
        public OeMetadataLevel MetadataLevel { get; set; }
        public OeSkipTokenNameValue[] SkipTokenNameValues { get; set; }
    }
}
