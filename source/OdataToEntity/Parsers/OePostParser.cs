using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity.Parsers
{
    public readonly struct OePostParser
    {
        private readonly IEdmModel _edmModel;
        private readonly Db.OeDataAdapter _dataAdapter;

        public OePostParser(IEdmModel edmModel)
        {
            _edmModel = edmModel;
            _dataAdapter = edmModel.GetDataAdapter(edmModel.EntityContainer);
        }

        public OeQueryContext CreateQueryContext(ODataUri odataUri, OeMetadataLevel metadataLevel)
        {
            var importSegment = (OperationImportSegment)odataUri.Path.FirstSegment;
            IEdmEntitySet entitySet = OeOperationHelper.GetEntitySet(importSegment.OperationImports.Single());
            if (entitySet == null)
                return null;

            OePropertyAccessor[] accessors = OePropertyAccessor.CreateFromType(_edmModel.GetClrType(entitySet), entitySet);
            Db.OeEntitySetAdapter entitySetAdapter = _dataAdapter.EntitySetAdapters.Find(entitySet);
            return new OeQueryContext(_edmModel, odataUri, entitySetAdapter, Array.Empty<OeParseNavigationSegment>(), 0, false, metadataLevel, OeModelBoundAttribute.No)
            {
                EntryFactory = new OeEntryFactory(entitySet, accessors, Array.Empty<OePropertyAccessor>()),
            };
        }
        public async Task ExecuteAsync(ODataUri odataUri, Stream requestStream, OeRequestHeaders headers, Stream responseStream, CancellationToken cancellationToken)
        {
            Object dataContext = null;
            try
            {
                dataContext = _dataAdapter.CreateDataContext();
                using (Db.OeAsyncEnumerator asyncEnumerator = GetAsyncEnumerator(odataUri, requestStream, headers, dataContext, out bool isScalar))
                {
                    if (isScalar)
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
                        OeQueryContext queryContext = CreateQueryContext(odataUri, headers.MetadataLevel);
                        if (queryContext == null)
                            await WriteCollectionAsync(_edmModel, odataUri, asyncEnumerator, responseStream).ConfigureAwait(false);
                        else
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
        public Db.OeAsyncEnumerator GetAsyncEnumerator(ODataUri odataUri, Stream requestStream, OeRequestHeaders headers, Object dataContext, out bool isScalar)
        {
            isScalar = true;
            var importSegment = (OperationImportSegment)odataUri.Path.LastSegment;
            IReadOnlyList<KeyValuePair<String, Object>> parameters = OeOperationHelper.GetParameters(_edmModel, importSegment, odataUri.ParameterAliasNodes, requestStream, headers.ContentType);

            IEdmOperationImport operationImport = importSegment.OperationImports.Single();
            IEdmEntitySet entitySet = OeOperationHelper.GetEntitySet(operationImport);
            if (entitySet == null)
            {
                if (operationImport.Operation.ReturnType == null)
                    return _dataAdapter.OperationAdapter.ExecuteProcedureNonQuery(dataContext, operationImport.Name, parameters);

                Type returnType = _edmModel.GetClrType(operationImport.Operation.ReturnType.Definition);
                if (operationImport.Operation.ReturnType.IsCollection())
                {
                    isScalar = false;
                    returnType = typeof(IEnumerable<>).MakeGenericType(returnType);
                }

                if (_edmModel.IsDbFunction(operationImport.Operation))
                    return _dataAdapter.OperationAdapter.ExecuteFunctionPrimitive(dataContext, operationImport.Name, parameters, returnType);
                else
                    return _dataAdapter.OperationAdapter.ExecuteProcedurePrimitive(dataContext, operationImport.Name, parameters, returnType);
            }

            isScalar = false;
            Db.OeEntitySetAdapter entitySetAdapter = _dataAdapter.EntitySetAdapters.Find(entitySet);
            if (_edmModel.IsDbFunction(operationImport.Operation))
                return _dataAdapter.OperationAdapter.ExecuteFunctionReader(dataContext, operationImport.Name, parameters, entitySetAdapter);
            else
                return _dataAdapter.OperationAdapter.ExecuteProcedureReader(dataContext, operationImport.Name, parameters, entitySetAdapter);
        }
        public static async Task WriteCollectionAsync(IEdmModel edmModel, ODataUri odataUri, Db.OeAsyncEnumerator asyncEnumerator, Stream responseStream)
        {
            var importSegment = (OperationImportSegment)odataUri.Path.LastSegment;
            IEdmOperationImport operationImport = importSegment.OperationImports.Single();
            Type returnType = edmModel.GetClrType(operationImport.Operation.ReturnType.Definition);

            IODataRequestMessage requestMessage = new Infrastructure.OeInMemoryMessage(responseStream, null);
            using (ODataMessageWriter messageWriter = new ODataMessageWriter(requestMessage,
                new ODataMessageWriterSettings() { EnableMessageStreamDisposal = false, ODataUri = odataUri }, edmModel))
            {
                IEdmTypeReference typeRef = OeEdmClrHelper.GetEdmTypeReference(edmModel, returnType);
                ODataCollectionWriter writer = messageWriter.CreateODataCollectionWriter(typeRef);
                writer.WriteStart(new ODataCollectionStart());

                while (await asyncEnumerator.MoveNextAsync().ConfigureAwait(false))
                {
                    Object value = asyncEnumerator.Current;
                    if (value != null && value.GetType().IsEnum)
                        value = value.ToString();

                    writer.WriteItem(value);
                }

                writer.WriteEnd();
            }
        }
    }
}
