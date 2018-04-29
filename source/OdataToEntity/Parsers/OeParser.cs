using Microsoft.OData.Edm;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Text;
using Microsoft.OData;
using Microsoft.OData.UriParser;

namespace OdataToEntity
{
    public readonly struct OeParser
    {
        private readonly Uri _baseUri;
        private readonly Db.OeDataAdapter _dataAdapter;
        private readonly IEdmModel _edmModel;

        public OeParser(Uri baseUri, Db.OeDataAdapter dataAdapter, IEdmModel edmModel)
        {
            _baseUri = baseUri;
            _dataAdapter = dataAdapter;
            _edmModel = edmModel;
        }

        public async Task<String> ExecuteBatchAsync(Stream requestStream, Stream responseStream, CancellationToken cancellationToken)
        {
            String contentType = GetConentType(requestStream, out ArraySegment<byte> readedBytes);
            var compositeStream = new CompositeReadStream(readedBytes, requestStream);
            await ExecuteBatchAsync(compositeStream, responseStream, contentType, cancellationToken).ConfigureAwait(false);
            return contentType;
        }
        public async Task ExecuteBatchAsync(Stream requestStream, Stream responseStream, String contentType, CancellationToken cancellationToken)
        {
            var paser = new Parsers.OeBatchParser(_baseUri, _dataAdapter, _edmModel);
            await paser.ExecuteAsync(requestStream, responseStream, contentType, cancellationToken).ConfigureAwait(false);
        }
        public async Task ExecuteGetAsync(Uri requestUri, OeRequestHeaders headers, Stream responseStream, CancellationToken cancellationToken)
        {
            var odataParser = new ODataUriParser(_edmModel, _baseUri, requestUri);
            odataParser.Resolver.EnableCaseInsensitive = true;
            ODataUri odataUri = odataParser.ParseUri();

            if (odataUri.Path.LastSegment is OperationImportSegment)
                await ExecuteOperationAsync(odataUri, headers, null, responseStream, cancellationToken).ConfigureAwait(false);
            else
                await ExecuteQueryAsync(odataUri, headers, responseStream, cancellationToken).ConfigureAwait(false);
        }
        public async Task ExecuteQueryAsync(ODataUri odataUri, OeRequestHeaders headers, Stream responseStream, CancellationToken cancellationToken)
        {
            var parser = new OeGetParser(_dataAdapter, _edmModel);
            await parser.ExecuteAsync(odataUri, headers, responseStream, cancellationToken).ConfigureAwait(false);
        }
        public async Task ExecuteOperationAsync(ODataUri odataUri, OeRequestHeaders headers, Stream requestStream, Stream responseStream, CancellationToken cancellationToken)
        {
            var parser = new OePostParser(_dataAdapter, _edmModel);
            await parser.ExecuteAsync(odataUri, requestStream, headers, responseStream, cancellationToken).ConfigureAwait(false);
        }
        public async Task ExecutePostAsync(Uri requestUri, OeRequestHeaders headers, Stream requestStream, Stream responseStream, CancellationToken cancellationToken)
        {
            var odataParser = new ODataUriParser(_edmModel, _baseUri, requestUri);
            odataParser.Resolver.EnableCaseInsensitive = true;
            ODataUri odataUri = odataParser.ParseUri();

            if (odataUri.Path.LastSegment.Identifier == "$batch")
                await ExecuteBatchAsync(responseStream, responseStream, headers.ContentType, cancellationToken).ConfigureAwait(false);
            else
                if (odataUri.Path.LastSegment is OperationImportSegment)
                    await ExecuteOperationAsync(odataUri, headers, requestStream, responseStream, cancellationToken).ConfigureAwait(false);
                else
                    await ExecuteQueryAsync(odataUri, headers, responseStream, cancellationToken).ConfigureAwait(false);
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
