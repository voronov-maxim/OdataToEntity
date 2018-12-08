using Microsoft.OData;
using System;

namespace OdataToEntity
{
    public enum OeMetadataLevel
    {
        None,
        Minimal,
        Full
    };

    public class OeRequestHeaders
    {
        public static readonly OeRequestHeaders JsonDefault = new OeRequestHeaders("application/json", OeMetadataLevel.Minimal, true, "utf-8");
        public static readonly OeRequestHeaders TextDefault = new OeRequestHeaders("text/plain", OeMetadataLevel.Minimal, true, "utf-8");

        protected OeRequestHeaders(OeRequestHeaders clone) : this(clone.MimeType, clone.MetadataLevel, clone.Streaming, clone.Charset)
        {
            MaxPageSize = clone.MaxPageSize;
            NavigationNextLink = clone.NavigationNextLink;
        }
        protected OeRequestHeaders(String mimeType, OeMetadataLevel metadataLevel, bool streaming, String charset)
        {
            MimeType = mimeType;
            MetadataLevel = metadataLevel;
            Streaming = streaming;
            Charset = charset;

            ContentType = GetContentType(mimeType, metadataLevel, streaming, charset);
        }

        private static String GetContentType(String mimeType, OeMetadataLevel metadataLevel, bool streaming, String charset)
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
            return $"{mimeType};odata.metadata={metadataArg};odata.streaming={streamingArg};charset={charset}";
        }
        private static int GetParameterValue(String acceptHeader, String parameterName, out int valueLength)
        {
            valueLength = 0;

            int i = acceptHeader.IndexOf(parameterName, StringComparison.OrdinalIgnoreCase);
            if (i == -1)
                return -1;

            if (i > 0)
                if (!Char.IsWhiteSpace(acceptHeader, i - 1) && acceptHeader[i - 1] != ';')
                    return -1;

            i += parameterName.Length;
            while (i < acceptHeader.Length && Char.IsWhiteSpace(acceptHeader, i))
                i++;

            if (acceptHeader[i] != '=')
                return -1;

            do
            {
                i++;
            }
            while (i < acceptHeader.Length && Char.IsWhiteSpace(acceptHeader, i));
            int start = i;

            do
            {
                i++;
                valueLength++;
            }
            while (i < acceptHeader.Length && !(Char.IsWhiteSpace(acceptHeader, i) || acceptHeader[i] == ';'));

            return start;
        }
        public static OeRequestHeaders Parse(String acceptHeader)
        {
            var metadataLevel = OeMetadataLevel.Minimal;
            bool streaming = true;

            int start;
            start = GetParameterValue(acceptHeader, "odata.metadata", out int valueLength);
            if (start != -1)
            {
                if (String.Compare(acceptHeader, start, "none", 0, "none".Length, StringComparison.OrdinalIgnoreCase) == 0)
                    metadataLevel = OeMetadataLevel.None;
                else if (String.Compare(acceptHeader, start, "full", 0, "full".Length, StringComparison.OrdinalIgnoreCase) == 0)
                    metadataLevel = OeMetadataLevel.Full;
            }

            start = GetParameterValue(acceptHeader, "charset", out valueLength);
            if (start != -1)
                if (String.Compare(acceptHeader, start, "utf-8", 0, "utf-8".Length, StringComparison.OrdinalIgnoreCase) != 0)
                    throw new NotSupportedException("charset=" + acceptHeader.Substring(start, valueLength) + " not supported");

            start = GetParameterValue(acceptHeader, "odata.streaming", out valueLength);
            if (start != -1)
                streaming = String.Compare(acceptHeader, start, "true", 0, "true".Length, StringComparison.OrdinalIgnoreCase) == 0;

            if (metadataLevel == JsonDefault.MetadataLevel && streaming == JsonDefault.Streaming)
                return JsonDefault;
            else
                return new OeRequestHeaders("application/json", metadataLevel, streaming, "utf-8");
        }
        public static OeRequestHeaders Parse(String acceptHeader, String preferHeader)
        {
            OeRequestHeaders requestHeaders = Parse(acceptHeader);
            if (String.IsNullOrEmpty(preferHeader))
                return requestHeaders;

            var message = new Infrastructure.OeInMemoryMessage(null, null);
            message.SetHeader("Prefer", preferHeader);
            ODataPreferenceHeader preferenceHeader = message.PreferHeader();
            if (preferenceHeader.MaxPageSize == null)
                return requestHeaders;

            return requestHeaders.SetMaxPageSize(preferenceHeader.MaxPageSize.GetValueOrDefault());
        }
        protected virtual OeRequestHeaders Clone() => new OeRequestHeaders(this);
        public OeRequestHeaders SetMaxPageSize(int maxPageSize)
        {
            if (MaxPageSize == maxPageSize)
                return this;

            OeRequestHeaders requestHeaders = Clone();
            requestHeaders.MaxPageSize = maxPageSize;
            return requestHeaders;
        }
        public OeRequestHeaders SetNavigationNextLink(bool navigationNextLink = false)
        {
            if (NavigationNextLink == navigationNextLink)
                return this;

            OeRequestHeaders requestHeaders = Clone();
            requestHeaders.NavigationNextLink = navigationNextLink;
            return requestHeaders;
        }

        public String Charset { get; }
        public String ContentType { get; }
        public OeMetadataLevel MetadataLevel { get; }
        public string MimeType { get; }
        public int MaxPageSize { get; private set; }
        public bool NavigationNextLink { get; private set; }
        public virtual string ResponseContentType { get; set; }
        public bool Streaming { get; }
    }
}
