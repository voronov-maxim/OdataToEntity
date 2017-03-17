using Microsoft.OData.Edm;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Text;

namespace OdataToEntity
{
    public sealed class OeParser
    {
        private readonly Uri _baseUri;
        private readonly IEdmModel _model;
        private readonly Db.OeDataAdapter _dataAdapter;

        public OeParser(Uri baseUri, Db.OeDataAdapter dataAdapter, IEdmModel model)
        {
            _baseUri = baseUri;
            _dataAdapter = dataAdapter;
            _model = model;
        }

        public async Task<String> ExecuteBatchAsync(Stream requestStream, Stream responseStream, CancellationToken cancellationToken)
        {
            ArraySegment<byte> readedBytes;
            String contentType = GetConentType(requestStream, out readedBytes);
            var compositeStream = new CompositeReadStream(readedBytes, requestStream);
            await ExecuteBatchAsync(compositeStream, responseStream, cancellationToken, contentType);
            return contentType;
        }
        public async Task ExecuteBatchAsync(Stream requestStream, Stream responseStream, CancellationToken cancellationToken, String contentType)
        {
            var paser = new Parsers.OeBatchParser(_baseUri, _dataAdapter, _model);
            await paser.ExecuteAsync(requestStream, responseStream, contentType, cancellationToken).ConfigureAwait(false);
        }
        public async Task ExecuteQueryAsync(Uri requestUri, OeRequestHeaders headers, Stream stream, CancellationToken cancellationToken)
        {
            var parser = new OeGetParser(_baseUri, _dataAdapter, _model);
            await parser.ExecuteAsync(requestUri, headers, stream, cancellationToken).ConfigureAwait(false);
        }
        public async Task ExecutePostAsync(Uri requestUri, Stream requestStream, Stream responseStream, CancellationToken cancellationToken)
        {
            var parser = new OePostParser(_baseUri, _dataAdapter, _model);
            await parser.ExecuteAsync(requestUri, requestStream, OeRequestHeaders.Default, responseStream, cancellationToken);
        }
        private static String GetConentType(Stream stream, out ArraySegment<byte> readedBytes)
        {
            var buffer = new byte[128];
            byte[] prefix = Encoding.UTF8.GetBytes("--batch");
            int i1 = 0;
            int i2 = 0;
            int position = 0;
            int b;
            while ((b = stream.ReadByte()) != -1)
            {
                buffer[position++] = (byte)b;
                if (i1 < prefix.Length)
                {
                    if (b == prefix[i1])
                        i1++;
                    else
                        i1 = 0;
                    i2 = position;
                }
                else
                {
                    if (b == '\r' || b == '\n')
                    {
                        i1 = i2 - ("batch".Length);
                        readedBytes = new ArraySegment<byte>(buffer, 0, position);
                        return "multipart/mixed;boundary=" + Encoding.UTF8.GetString(buffer, i1, position - i1 - 1);
                    }
                }
            }

            throw new InvalidDataException("is not batch stream");
        }
    }
}
