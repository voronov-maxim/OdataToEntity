using System;
using System.IO;

namespace OdataToEntity
{
    public sealed class OeResponse
    {
        private readonly String _contentType;
        private readonly Stream _stream;

        public OeResponse(Stream stream, String contentType)
        {
            _stream = stream;
            _contentType = contentType;
        }

        public String ContentType => _contentType;
        public Stream Stream => _stream;
    }
}
