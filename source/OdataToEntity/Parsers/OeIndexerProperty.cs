using System;

namespace OdataToEntity.Parsers
{
    public interface OeIndexerProperty
    {
        Object this[String name] { get; }
    }
}
