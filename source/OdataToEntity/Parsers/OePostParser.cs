using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using OdataToEntity.Parsers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity
{
    public readonly struct OePostParser
    {
        private readonly IEdmModel _edmModel;
        private readonly Db.OeDataAdapter _dataAdapter;

        public OePostParser(Db.OeDataAdapter dataAdapter, IEdmModel edmModel)
        {
            _dataAdapter = dataAdapter;
            _edmModel = edmModel;
        }

        public OeQueryContext CreateQueryContext(ODataUri odataUri, OeMetadataLevel metadataLevel, Type returnClrType)
        {
            String entitySetName = _dataAdapter.EntitySetAdapters.FindByClrType(returnClrType).EntitySetName;
            IEdmEntitySet entitySet = _edmModel.FindDeclaredEntitySet(entitySetName);
            OePropertyAccessor[] accessors = OePropertyAccessor.CreateFromType(returnClrType, entitySet);

            Db.OeEntitySetAdapter entitySetAdapter = _dataAdapter.EntitySetAdapters.FindByEntitySetName(entitySet.Name);
            return new OeQueryContext(_edmModel, odataUri, entitySet, null, false, 0, false,
                _dataAdapter.IsDatabaseNullHighestValue, metadataLevel, entitySetAdapter)
                {
                    EntryFactory = OeEntryFactory.CreateEntryFactory(entitySet, accessors),
                };
        }
        public async Task ExecuteAsync(ODataUri odataUri, Stream requestStream, OeRequestHeaders headers, Stream responseStream, CancellationToken cancellationToken)
        {
            Object dataContext = null;
            try
            {
                dataContext = _dataAdapter.CreateDataContext();
                using (Db.OeAsyncEnumerator asyncEnumerator = GetAsyncEnumerator(odataUri, requestStream, headers, dataContext, out Type returnClrType))
                {
                    if (returnClrType == null || returnClrType.IsPrimitive || returnClrType == typeof(String))
                    {
                        if (await asyncEnumerator.MoveNextAsync().ConfigureAwait(false) && asyncEnumerator.Current != null)
                        {
                            headers.ResponseContentType = OeRequestHeaders.TextDefault.ContentType;
                            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(asyncEnumerator.Current.ToString());
                            responseStream.Write(buffer, 0, buffer.Length);
                        }
                        else
                            headers.ResponseContentType = null;
                    }
                    else
                    {
                        OeQueryContext queryContext = CreateQueryContext(odataUri, headers.MetadataLevel, returnClrType);
                        await Writers.OeGetWriter.SerializeAsync(queryContext, asyncEnumerator, headers.ContentType, responseStream).ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                if (dataContext != null)
                    _dataAdapter.CloseDataContext(dataContext);
            }
        }
        private void FillParameters(List<KeyValuePair<String, Object>> parameters, Stream requestStream, IEdmOperation operation, String contentType)
        {
            if (!operation.Parameters.Any())
                return;

            IODataRequestMessage requestMessage = new OeInMemoryMessage(requestStream, contentType);
            var settings = new ODataMessageReaderSettings() { EnableMessageStreamDisposal = false };
            using (var messageReader = new ODataMessageReader(requestMessage, settings, _edmModel))
            {
                ODataParameterReader parameterReader = messageReader.CreateODataParameterReader(operation);
                while (parameterReader.Read())
                {
                    Object value;
                    switch (parameterReader.State)
                    {
                        case ODataParameterReaderState.Value:
                            {
                                value = OeEdmClrHelper.GetValue(_edmModel, parameterReader.Value);
                                break;
                            }
                        case ODataParameterReaderState.Collection:
                            {
                                ODataCollectionReader collectionReader = parameterReader.CreateCollectionReader();
                                value = OeEdmClrHelper.GetValue(_edmModel, ReadCollection(collectionReader));
                                break;
                            }
                        case ODataParameterReaderState.Resource:
                            {
                                ODataReader reader = parameterReader.CreateResourceReader();
                                value = OeEdmClrHelper.GetValue(_edmModel, ReadResource(reader));
                                break;
                            }
                        case ODataParameterReaderState.ResourceSet:
                            {
                                ODataReader reader = parameterReader.CreateResourceSetReader();
                                value = OeEdmClrHelper.GetValue(_edmModel, ReadResourceSet(reader));
                                break;
                            }
                        default:
                            continue;
                    }

                    parameters.Add(new KeyValuePair<String, Object>(parameterReader.Name, value));
                }
            }
        }
        public Db.OeAsyncEnumerator GetAsyncEnumerator(ODataUri odataUri, Stream requestStream, OeRequestHeaders headers, Object dataContext, out Type returnClrType)
        {
            var importSegment = (OperationImportSegment)odataUri.Path.LastSegment;

            List<KeyValuePair<String, Object>> parameters = GetParameters(importSegment, requestStream, headers.ContentType);
            IEdmOperationImport operationImport = importSegment.OperationImports.Single();

            returnClrType = null;
            if (operationImport.Operation.ReturnType != null)
            {
                IEdmTypeReference returnEdmTypeReference = operationImport.Operation.ReturnType;
                if (returnEdmTypeReference is IEdmCollectionTypeReference)
                    returnEdmTypeReference = (returnEdmTypeReference.Definition as IEdmCollectionType).ElementType;
                returnClrType = OeEdmClrHelper.GetClrType(_edmModel, returnEdmTypeReference.Definition);
            }

            if (_edmModel.IsDbFunction(operationImport.Operation))
                return _dataAdapter.OperationAdapter.ExecuteFunction(dataContext, operationImport.Name, parameters, returnClrType);
            else
                return _dataAdapter.OperationAdapter.ExecuteProcedure(dataContext, operationImport.Name, parameters, returnClrType);
        }
        private List<KeyValuePair<String, Object>> GetParameters(OperationImportSegment importSegment, Stream requestStream, String contentType)
        {
            var parameters = new List<KeyValuePair<String, Object>>();

            foreach (OperationSegmentParameter segmentParameter in importSegment.Parameters)
            {
                Object value;
                if (segmentParameter.Value is ConstantNode constantNode)
                    value = OeEdmClrHelper.GetValue(_edmModel, constantNode.Value);
                else
                    value = OeEdmClrHelper.GetValue(_edmModel, segmentParameter.Value);
                parameters.Add(new KeyValuePair<String, Object>(segmentParameter.Name, value));
            }

            var operation = (EdmOperation)importSegment.OperationImports.Single().Operation;
            if (parameters.Count == 0 && requestStream != null)
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
