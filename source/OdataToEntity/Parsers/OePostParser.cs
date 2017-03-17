using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using OdataToEntity.ModelBuilder;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity
{
    public sealed class OePostParser
    {
        private readonly Uri _baseUri;
        private readonly IEdmModel _model;
        private readonly Db.OeDataAdapter _dataAdapter;

        public OePostParser(Uri baseUri, Db.OeDataAdapter dataAdapter, IEdmModel model)
        {
            _baseUri = baseUri;
            _dataAdapter = dataAdapter;
            _model = model;
        }

        public async Task ExecuteAsync(Uri requestUri, Stream requestStream, OeRequestHeaders headers, Stream responseStream, CancellationToken cancellationToken)
        {
            var odataParser = new ODataUriParser(_model, _baseUri, requestUri);
            odataParser.Resolver.EnableCaseInsensitive = true;
            ODataUri odataUri = odataParser.ParseUri();
            var importSegment = (OperationImportSegment)odataUri.Path.LastSegment;

            List<KeyValuePair<String, Object>> parameters = GetParameters(importSegment, requestStream, headers.ContentType);
            Object dataContext = null;
            try
            {
                dataContext = _dataAdapter.CreateDataContext();

                var operation = (EdmOperation)importSegment.OperationImports.Single().Operation;

                String procedureName;
                if (String.IsNullOrEmpty(operation.Namespace))
                    procedureName = operation.Name;
                else
                    procedureName = operation.Namespace + "." + operation.Name;

                Type returnClrType = null;
                if (operation.ReturnType != null)
                {
                    IEdmTypeReference returnEdmTypeReference = operation.ReturnType;
                    if (returnEdmTypeReference is IEdmCollectionTypeReference)
                        returnEdmTypeReference = (returnEdmTypeReference.Definition as IEdmCollectionType).ElementType;
                    returnClrType = OeEdmClrHelper.GetClrType(_model, returnEdmTypeReference.Definition);
                }

                using (Db.OeEntityAsyncEnumerator asyncEnumerator = _dataAdapter.ExecuteProcedure(dataContext, procedureName, parameters, returnClrType))
                {
                    if (returnClrType == null)
                    {
                        if (await asyncEnumerator.MoveNextAsync() && asyncEnumerator.Current != null)
                        {
                            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(asyncEnumerator.Current.ToString());
                            responseStream.Write(buffer, 0, buffer.Length);
                        }
                    }
                    else
                    {
                        String entitySetName = _dataAdapter.EntitySetMetaAdapters.FindByClrType(returnClrType).EntitySetName;
                        IEdmEntitySet entitySet = _model.FindDeclaredEntitySet(entitySetName);
                        Parsers.OePropertyAccessor[] accessors = Parsers.OePropertyAccessor.CreateFromType(returnClrType, entitySet);
                        Parsers.OeEntryFactory entryFactory = Parsers.OeEntryFactory.CreateEntryFactory(entitySet, accessors);

                        var parseUriContext = new Parsers.OeParseUriContext(_model, odataUri, entitySet, null, false)
                        {
                            EntryFactory = entryFactory,
                            Headers = headers
                        };
                        await Writers.OeGetWriter.SerializeAsync(_baseUri, parseUriContext, asyncEnumerator, responseStream).ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                if (dataContext != null)
                    _dataAdapter.CloseDataContext(dataContext);
            }

            await Task.CompletedTask;
        }
        private void FillParameters(List<KeyValuePair<String, Object>> parameters, Stream requestStream, IEdmOperation operation, String contentType)
        {
            if (!operation.Parameters.Any())
                return;

            IODataRequestMessage requestMessage = new OeInMemoryMessage(requestStream, contentType);
            var settings = new ODataMessageReaderSettings() { EnableMessageStreamDisposal = false };
            using (var messageReader = new ODataMessageReader(requestMessage, settings, _model))
            {
                ODataParameterReader parameterReader = messageReader.CreateODataParameterReader(operation);
                while (parameterReader.Read())
                {
                    Object value;
                    switch (parameterReader.State)
                    {
                        case ODataParameterReaderState.Value:
                            {
                                value = OeEdmClrHelper.GetValue(_model, parameterReader.Value);
                                break;
                            }
                        case ODataParameterReaderState.Collection:
                            {
                                ODataCollectionReader collectionReader = parameterReader.CreateCollectionReader();
                                value = OeEdmClrHelper.GetValue(_model, ReadCollection(collectionReader));
                                break;
                            }
                        case ODataParameterReaderState.Resource:
                            {
                                ODataReader reader = parameterReader.CreateResourceReader();
                                value = OeEdmClrHelper.GetValue(_model, ReadResource(reader));
                                break;
                            }
                        case ODataParameterReaderState.ResourceSet:
                            {
                                ODataReader reader = parameterReader.CreateResourceSetReader();
                                value = OeEdmClrHelper.GetValue(_model, ReadResourceSet(reader));
                                break;
                            }
                        default:
                            continue;
                    }

                    parameters.Add(new KeyValuePair<String, Object>(parameterReader.Name, value));
                }
            }
        }
        private List<KeyValuePair<String, Object>> GetParameters(OperationImportSegment importSegment, Stream requestStream, String contentType)
        {
            var parameters = new List<KeyValuePair<String, Object>>();

            foreach (OperationSegmentParameter segmentParameter in importSegment.Parameters)
            {
                Object value;
                var constantNode = segmentParameter.Value as ConstantNode;
                if (constantNode == null)
                    value = OeEdmClrHelper.GetValue(_model, segmentParameter.Value);
                else
                    value = OeEdmClrHelper.GetValue(_model, constantNode.Value);
                parameters.Add(new KeyValuePair<String, Object>(segmentParameter.Name, value));
            }

            var operation = (EdmOperation)importSegment.OperationImports.Single().Operation;
            if (parameters.Count == 0)
                FillParameters(parameters, requestStream, operation, contentType);
            OrderParameters(operation.Parameters, parameters);

            return parameters;
        }
        private static void OrderParameters(IEnumerable<IEdmOperationParameter> operationParameters, List<KeyValuePair<String, Object>> parameters)
        {
            int pos = 0;
            foreach (IEdmOperationParameter operationParameter in operationParameters)
            {
                for (int i = pos; i < parameters.Count; i++)
                    if (String.Compare(operationParameter.Name, parameters[i].Key, StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        if (i != pos)
                        {
                            KeyValuePair<String, Object> temp = parameters[pos];
                            parameters[pos] = parameters[i];
                            parameters[i] = temp;
                        }
                        pos++;
                        break;
                    }
            }
        }
        private static ODataCollectionValue ReadCollection(ODataCollectionReader collectionReader)
        {
            var items = new List<Object>();
            while (collectionReader.Read())
            {
                if (collectionReader.State == ODataCollectionReaderState.Completed)
                    break;

                if (collectionReader.State == ODataCollectionReaderState.Value)
                    items.Add(collectionReader.Item);
            }

            return new ODataCollectionValue() { Items = items };
        }
        private static ODataResource ReadResource(ODataReader reader)
        {
            ODataResource resource = null;
            while (reader.Read())
                if (reader.State == ODataReaderState.ResourceEnd)
                    resource = (ODataResource)reader.Item;
            return resource;
        }
        private static ODataCollectionValue ReadResourceSet(ODataReader reader)
        {
            var items = new List<ODataResource>();
            while (reader.Read())
                if (reader.State == ODataReaderState.ResourceEnd)
                    items.Add((ODataResource)reader.Item);

            return new ODataCollectionValue() { Items = items };
        }
    }
}
