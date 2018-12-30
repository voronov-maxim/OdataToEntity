using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity
{
    public readonly struct OeParser
    {
        private sealed class RefModelUriResolver : ODataUriResolver
        {
            public RefModelUriResolver()
            {
                EnableCaseInsensitive = true;
            }

            public override IEdmNavigationSource ResolveNavigationSource(IEdmModel model, String identifier)
            {
                return OeEdmClrHelper.GetEntitySet(model, identifier);
            }
            public override IEnumerable<IEdmOperationImport> ResolveOperationImports(IEdmModel model, String identifier)
            {
                IEnumerable<IEdmOperationImport> operationImports = model.FindDeclaredOperationImports(identifier);
                if (operationImports != null && operationImports.Any())
                    return operationImports;

                foreach (IEdmEntityContainerElement element in model.EntityContainer.Elements)
                    if (element is IEdmOperationImport operationImport &&
                        String.Compare(operationImport.Name, identifier, StringComparison.OrdinalIgnoreCase) == 0)
                        return new[] { operationImport };

                foreach (IEdmModel refModel in model.ReferencedModels)
                    if (refModel.EntityContainer != null)
                    {
                        operationImports = ResolveOperationImports(refModel, identifier);
                        if (operationImports != null && operationImports.Any())
                            return operationImports;
                    }

                return null;
            }
        }

        private sealed class ServiceProvider : IServiceProvider
        {
            private static readonly ODataUriParserSettings _uriParserSettings = new ODataUriParserSettings();
            private static readonly UriPathParser _uriPathParser = new UriPathParser(_uriParserSettings);
            private static readonly ODataSimplifiedOptions _simplifiedOptions = new ODataSimplifiedOptions();
            private static readonly ODataUriResolver _uriResolver = new RefModelUriResolver();
            public static readonly ServiceProvider Instance = new ServiceProvider();

            private ServiceProvider()
            {
            }

            public Object GetService(Type serviceType)
            {
                if (serviceType == typeof(ODataUriResolver))
                    return _uriResolver;
                if (serviceType == typeof(ODataSimplifiedOptions))
                    return _simplifiedOptions;
                if (serviceType == typeof(ODataUriParserSettings))
                    return _uriParserSettings;
                if (serviceType == typeof(UriPathParser))
                    return _uriPathParser;

                throw new InvalidOperationException("ServiceProvider not found type " + serviceType.FullName);
            }
        }

        private readonly Uri _baseUri;
        private readonly IEdmModel _edmModel;

        public OeParser(Uri baseUri, IEdmModel edmModel)
        {
            _baseUri = baseUri;
            _edmModel = edmModel;
        }
        [Obsolete("Use OeParser(Uri, IEdmModel)", true)]
        public OeParser(Uri baseUri, Db.OeDataAdapter dataAdapter, IEdmModel edmModel) : this(baseUri, edmModel)
        {
        }

        public async Task<String> ExecuteBatchAsync(Stream requestStream, Stream responseStream, CancellationToken cancellationToken)
        {
            String contentType = GetConentType(requestStream, out ArraySegment<byte> readedBytes);
            var compositeStream = new Infrastructure.CompositeReadStream(readedBytes, requestStream);
            await ExecuteBatchAsync(compositeStream, responseStream, contentType, cancellationToken).ConfigureAwait(false);
            return contentType;
        }
        public async Task ExecuteBatchAsync(Stream requestStream, Stream responseStream, String contentType, CancellationToken cancellationToken)
        {
            var paser = new Parsers.OeBatchParser(_baseUri, _edmModel);
            await paser.ExecuteAsync(requestStream, responseStream, contentType, cancellationToken).ConfigureAwait(false);
        }
        public async Task ExecuteGetAsync(Uri requestUri, OeRequestHeaders headers, Stream responseStream, CancellationToken cancellationToken)
        {
            ODataUri odataUri = ParseUri(_edmModel, _baseUri, requestUri);
            if (odataUri.Path.LastSegment is OperationImportSegment)
                await ExecuteOperationAsync(odataUri, headers, null, responseStream, cancellationToken).ConfigureAwait(false);
            else
                await ExecuteQueryAsync(odataUri, headers, responseStream, cancellationToken).ConfigureAwait(false);
        }
        public async Task ExecuteQueryAsync(ODataUri odataUri, OeRequestHeaders headers, Stream responseStream, CancellationToken cancellationToken)
        {
            var parser = new Parsers.OeGetParser(_edmModel.GetEdmModel(odataUri.Path));
            await parser.ExecuteAsync(odataUri, headers, responseStream, cancellationToken).ConfigureAwait(false);
        }
        public async Task ExecuteOperationAsync(ODataUri odataUri, OeRequestHeaders headers, Stream requestStream, Stream responseStream, CancellationToken cancellationToken)
        {
            var parser = new Parsers.OePostParser(_edmModel.GetEdmModel(odataUri.Path));
            await parser.ExecuteAsync(odataUri, requestStream, headers, responseStream, cancellationToken).ConfigureAwait(false);
        }
        public async Task ExecutePostAsync(Uri requestUri, OeRequestHeaders headers, Stream requestStream, Stream responseStream, CancellationToken cancellationToken)
        {
            ODataUri odataUri = OeParser.ParseUri(_edmModel, _baseUri, requestUri);
            if (odataUri.Path.LastSegment.Identifier == "$batch")
                await ExecuteBatchAsync(requestStream, responseStream, headers.ContentType, cancellationToken).ConfigureAwait(false);
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
        public static ODataPath ParsePath(IEdmModel model, Uri serviceRoot, Uri uri)
        {
            var uriParser = new ODataUriParser(model, serviceRoot, uri, ServiceProvider.Instance);
            return uriParser.ParsePath();
        }
        public static ODataUri ParseUri(IEdmModel model, Uri serviceRoot, Uri uri)
        {
            var uriParser = new ODataUriParser(model, serviceRoot, uri, ServiceProvider.Instance);
            ODataPath path = uriParser.ParsePath();
            IEdmModel refModel = model.GetEdmModel(path);
            if (refModel == model)
                return ParseUri(uriParser, path, serviceRoot);

            uriParser = new ODataUriParser(refModel, serviceRoot, uri, ServiceProvider.Instance);
            return ParseUri(uriParser, path, serviceRoot);
        }
        private static ODataUri ParseUri(ODataUriParser parser, ODataPath path, Uri serviceRoot)
        {
            return new ODataUri()
            {
                Apply = parser.ParseApply(),
                Compute = parser.ParseCompute(),
                DeltaToken = parser.ParseDeltaToken(),
                Filter = parser.ParseFilter(),
                OrderBy = parser.ParseOrderBy(),
                Path = path,
                QueryCount = parser.ParseCount(),
                Search = parser.ParseSearch(),
                SelectAndExpand = parser.ParseSelectAndExpand(),
                ServiceRoot = serviceRoot,
                Skip = parser.ParseSkip(),
                SkipToken = parser.ParseSkipToken(),
                Top = parser.ParseTop()
            };
        }
    }
}
