using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.Json;
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
        private sealed class ContainerBuilder : IContainerBuilder, IServiceProvider
        {
            private ContainerBuilder()
            {
                JsonReaderFactory = null!;
            }

            public IContainerBuilder AddService(ServiceLifetime lifetime, Type serviceType, Type implementationType)
            {
                if (serviceType == typeof(IJsonReaderFactory))
                    JsonReaderFactory = (IJsonReaderFactory)Activator.CreateInstance(implementationType)!;
                return this;
            }
            public IContainerBuilder AddService(ServiceLifetime lifetime, Type serviceType, Func<IServiceProvider, object> implementationFactory)
            {
                return this;
            }
            public IServiceProvider BuildContainer()
            {
                return this;
            }
            public static ContainerBuilder Create()
            {
                var containerBuilder = new ContainerBuilder();
                containerBuilder.AddDefaultODataServices();
                return containerBuilder;
            }
            public object GetService(Type serviceType)
            {
                throw new NotImplementedException();
            }

            public IJsonReaderFactory JsonReaderFactory { get; private set; }
        }

        private sealed class RefModelUriResolver : ODataUriResolver
        {
            public RefModelUriResolver()
            {
                EnableCaseInsensitive = true;
            }

            public override IEnumerable<IEdmOperation> ResolveBoundOperations(IEdmModel edmModel, String identifier, IEdmType bindingType)
            {
                if (identifier.IndexOf('.') != -1)
                    return base.ResolveBoundOperations(edmModel, identifier, bindingType);

                var edmOperations = new List<IEdmOperation>();

                foreach (IEdmSchemaElement element in edmModel.SchemaElements)
                    if (element is IEdmOperation edmOperation &&
                        edmOperation.IsBound &&
                        String.Compare(edmOperation.Name, identifier, StringComparison.OrdinalIgnoreCase) == 0 &&
                        edmOperation.HasEquivalentBindingType(bindingType))
                        edmOperations.Add(edmOperation);

                foreach (IEdmModel refModel in edmModel.ReferencedModels)
                    if (refModel.EntityContainer != null && refModel is EdmModel)
                        edmOperations.AddRange(ResolveBoundOperations(refModel, identifier, bindingType));

                return edmOperations;
            }
            public override IEdmNavigationSource? ResolveNavigationSource(IEdmModel model, String identifier)
            {
                return OeEdmClrHelper.GetEntitySetOrNull(model, identifier, EnableCaseInsensitive ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
            }
            public override IEnumerable<IEdmOperationImport>? ResolveOperationImports(IEdmModel edmModel, String identifier)
            {
                IEnumerable<IEdmOperationImport>? operationImports = edmModel.FindDeclaredOperationImports(identifier);
                if (operationImports != null && operationImports.Any())
                    return operationImports;

                foreach (IEdmEntityContainerElement element in edmModel.EntityContainer.Elements)
                    if (element is IEdmOperationImport operationImport &&
                        String.Compare(operationImport.Name, identifier, StringComparison.OrdinalIgnoreCase) == 0)
                        return new[] { operationImport };

                foreach (IEdmModel refModel in edmModel.ReferencedModels)
                    if (refModel.EntityContainer != null && refModel is EdmModel)
                    {
                        operationImports = ResolveOperationImports(refModel, identifier);
                        if (operationImports != null && operationImports.Any())
                            return operationImports;
                    }

                return null;
            }
        }

        private sealed class ServiceProviderImpl : IServiceProvider
        {
            private static readonly ContainerBuilder _containerBuilder = ContainerBuilder.Create();
            private static readonly ODataMediaTypeResolver _mediaTypeResolver = new ODataMediaTypeResolver();
            private static readonly ODataPayloadValueConverter _payloadValueConverter = new ODataPayloadValueConverter();
            private static readonly ODataSimplifiedOptions _simplifiedOptions = new ODataSimplifiedOptions();
            private static readonly ODataUriParserSettings _uriParserSettings = new ODataUriParserSettings();
            private static readonly UriPathParser _uriPathParser = new UriPathParser(_uriParserSettings);
            private static readonly ODataUriResolver _uriResolver = new RefModelUriResolver();
            public static readonly ServiceProviderImpl Instance = new ServiceProviderImpl();

            private ServiceProviderImpl()
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
                if (serviceType == typeof(ODataMediaTypeResolver))
                    return _mediaTypeResolver;
                if (serviceType == typeof(ODataMessageInfo))
                    return new ODataMessageInfo();
                if (serviceType == typeof(ODataPayloadValueConverter))
                    return _payloadValueConverter;
                if (serviceType == typeof(IJsonWriterFactory))
                    return new DefaultJsonWriterFactory(ODataStringEscapeOption.EscapeOnlyControls);
                if (serviceType == typeof(IJsonReaderFactory))
                    return _containerBuilder.JsonReaderFactory;
                if (serviceType == typeof(ODataMessageReaderSettings))
                    return new ODataMessageReaderSettings();

                throw new InvalidOperationException("ServiceProvider not found type " + serviceType.FullName);
            }
        }

        private readonly IServiceProvider? _serviceProvider;

        public OeParser(Uri baseUri, IEdmModel edmModel) : this(baseUri, edmModel, null, null)
        {
        }
        public OeParser(Uri baseUri, IEdmModel edmModel, Query.OeModelBoundProvider? modelBoundProvider, IServiceProvider? serviceProvider = null)
        {
            BaseUri = baseUri;
            EdmModel = edmModel;
            ModelBoundProvider = modelBoundProvider;
            _serviceProvider = serviceProvider;
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
            var paser = new Parsers.OeBatchParser(BaseUri, EdmModel, _serviceProvider);
            await paser.ExecuteAsync(requestStream, responseStream, contentType, cancellationToken).ConfigureAwait(false);
        }
        public async Task ExecuteGetAsync(Uri requestUri, OeRequestHeaders headers, Stream responseStream, CancellationToken cancellationToken)
        {
            ODataUri odataUri = ParseUri(EdmModel, BaseUri, requestUri, _serviceProvider);
            if (odataUri.Path.LastSegment is OperationImportSegment)
                await ExecuteOperationAsync(odataUri, headers, null, responseStream, cancellationToken).ConfigureAwait(false);
            else
                await ExecuteQueryAsync(odataUri, headers, responseStream, cancellationToken).ConfigureAwait(false);
        }
        public async Task ExecuteQueryAsync(ODataUri odataUri, OeRequestHeaders headers, Stream responseStream, CancellationToken cancellationToken)
        {
            var parser = new Parsers.OeGetParser(EdmModel.GetEdmModel(odataUri.Path), _serviceProvider, ModelBoundProvider);
            await parser.ExecuteAsync(odataUri, headers, responseStream, cancellationToken).ConfigureAwait(false);
        }
        public async Task ExecuteOperationAsync(ODataUri odataUri, OeRequestHeaders headers, Stream? requestStream, Stream responseStream, CancellationToken cancellationToken)
        {
            var parser = new Parsers.OePostParser(EdmModel.GetEdmModel(odataUri.Path), _serviceProvider);
            await parser.ExecuteAsync(odataUri, requestStream, headers, responseStream, cancellationToken).ConfigureAwait(false);
        }
        public async Task ExecutePostAsync(Uri requestUri, OeRequestHeaders headers, Stream requestStream, Stream responseStream, CancellationToken cancellationToken)
        {
            ODataUri odataUri = ParseUri(EdmModel, BaseUri, requestUri, _serviceProvider);
            if (odataUri.Path.LastSegment.Identifier == "$batch")
                await ExecuteBatchAsync(requestStream, responseStream, headers.ContentType, cancellationToken).ConfigureAwait(false);
            else if (odataUri.Path.LastSegment is OperationImportSegment)
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
        public static ODataPath ParsePath(IEdmModel edmModel, Uri serviceRoot, Uri uri)
        {
            var uriParser = new ODataUriParser(edmModel, serviceRoot, uri, ServiceProviderImpl.Instance);
            return uriParser.ParsePath();
        }
        public static ODataUri ParseUri(IEdmModel edmModel, Uri relativeUri, IServiceProvider? serviceProvider = null)
        {
            return ParseUri(edmModel, null, relativeUri, serviceProvider);
        }
        public static ODataUri ParseUri(IEdmModel edmModel, Uri? serviceRoot, Uri uri, IServiceProvider? serviceProvider = null)
        {
            serviceProvider = serviceProvider ?? ServiceProviderImpl.Instance;
            ODataUriParser uriParser;
            if (serviceRoot == null)
                uriParser = new ODataUriParser(edmModel, uri, serviceProvider);
            else
                uriParser = new ODataUriParser(edmModel, serviceRoot, uri, serviceProvider);
            try
            {
                return uriParser.ParseUri();
            }
            catch (ODataException e) when (e.Message.StartsWith("Could not find a property named", StringComparison.Ordinal))
            {
                //fix test ApplyGroupByAggregateOrderBy bug #703
                if (serviceRoot == null)
                    uriParser = new ODataUriParser(edmModel, uri, serviceProvider);
                else
                    uriParser = new ODataUriParser(edmModel, serviceRoot, uri, serviceProvider);
                uriParser.ParseApply();
                return uriParser.ParseUri();
            }
        }

        public Uri BaseUri { get; }
        public IEdmModel EdmModel { get; }
        public Query.OeModelBoundProvider? ModelBoundProvider { get; }
        public static IServiceProvider ServiceProvider => ServiceProviderImpl.Instance;
    }
}
