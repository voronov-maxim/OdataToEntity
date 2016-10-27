using Microsoft.OData;
using System;
using System.Collections.Generic;
using System.Net.Http.Headers;

namespace OdataToEntity
{
    public enum OeMetadataLevel
    {
        None,
        Minimal,
        Full
    };

    public sealed class OeRequestHeaders
    {
        private readonly String _charset;
        private readonly String _contentType;
        private readonly String _mediaType;
        private readonly OeMetadataLevel _metadataLevel;

        public OeRequestHeaders()
            : this(OeMetadataLevel.Minimal, true)
        {
        }
        public OeRequestHeaders(OeMetadataLevel metadataLevel, bool streaming)
            : this(metadataLevel, streaming, "utf-8")
        {

        }
        public OeRequestHeaders(OeMetadataLevel metadataLevel, bool streaming, String charset)
            : this(metadataLevel, true, charset, "application/json")
        {
        }
        public OeRequestHeaders(OeMetadataLevel metadataLevel, bool streaming, String charset, String mediaType)
            : this(metadataLevel, streaming, charset, mediaType, GetContentType(metadataLevel, streaming, charset, mediaType))
        {
        }
        private OeRequestHeaders(OeMetadataLevel metadataLevel, bool streaming, String charset, String mediaType, String contentType)
        {
            _metadataLevel = metadataLevel;
            _charset = charset;
            _mediaType = mediaType;
            _contentType = contentType;
        }

        private static String GetContentType(OeMetadataLevel metadataLevel, bool streaming, String charset, String mediaType)
        {
            String metadataArg;
            switch (metadataLevel)
            {
                case OeMetadataLevel.None:
                    metadataArg = "none";
                    break;
                case OeMetadataLevel.Full:
                    metadataArg = "full";
                    break;
                default:
                    metadataArg = "minimal";
                    break;
            }

            String streamingArg = streaming ? "true" : "false";
            return $"{mediaType};odata.metadata={metadataArg};odata.streaming={streamingArg};charset={charset}";
        }
        public static OeRequestHeaders Parse(string acceptHeader)
        {
            var metadataLevel = OeMetadataLevel.Minimal;
            bool streaming = true;
            String charset = "utf-8";

            MediaTypeHeaderValue mediaType;
            if (!MediaTypeHeaderValue.TryParse(acceptHeader, out mediaType))
                return new OeRequestHeaders(metadataLevel, streaming, charset);

            foreach (NameValueHeaderValue parameter in mediaType.Parameters)
            {
                if (String.Compare(parameter.Name, "odata.metadata", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    if (String.Compare(parameter.Value, "none", StringComparison.OrdinalIgnoreCase) == 0)
                        metadataLevel = OeMetadataLevel.None;
                    else if (String.Compare(parameter.Value, "full", StringComparison.OrdinalIgnoreCase) == 0)
                        metadataLevel = OeMetadataLevel.Full;
                }
                else if (String.Compare(parameter.Name, "charset", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    charset = parameter.Value;
                }
                else if (String.Compare(parameter.Name, "streaming", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    Boolean.TryParse(parameter.Value, out streaming);
                }
            }
            return new OeRequestHeaders(metadataLevel, streaming, charset, mediaType.MediaType);
        }

        public String Charset => _charset;
        public String ContentType => _contentType;
        public String MediaType => _mediaType;
        public OeMetadataLevel MetadataLevel => _metadataLevel;
    }
}
