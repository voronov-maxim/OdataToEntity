using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace OdataToEntity.Parsers
{
    public static class OeOperationHelper
    {
        public static IAsyncEnumerable<Object?> ApplyBoundFunction(OeQueryContext queryContext)
        {
            if (queryContext.ODataUri.Path.LastSegment is OperationSegment operationSegment)
            {
                var edmFunction = (IEdmFunction)operationSegment.Operations.First();
                MethodInfo methodInfo = queryContext.EdmModel.GetMethodInfo(edmFunction);

                IReadOnlyList<KeyValuePair<String, Object?>> parameterList = OeOperationHelper.GetParameters(
                    queryContext.EdmModel, operationSegment, queryContext.ODataUri.ParameterAliasNodes);

                Db.OeBoundFunctionParameter boundFunctionParameter = OeOperationHelper.CreateBoundFunctionParameter(queryContext);

                var parameters = new Object?[parameterList.Count + 1];
                parameters[0] = boundFunctionParameter;
                for (int i = 1; i < parameters.Length; i++)
                    parameters[i] = parameterList[i - 1].Value;

                Object? result = methodInfo.Invoke(null, parameters);
                queryContext.EntryFactory = boundFunctionParameter.CreateEntryFactoryFromTuple();
                if (result is IEnumerable enumerable)
                    return Infrastructure.AsyncEnumeratorHelper.ToAsyncEnumerable(enumerable);

                return Infrastructure.AsyncEnumeratorHelper.ToAsyncEnumerable(Task.FromResult(result));
            }

            throw new InvalidOperationException("Path last segment not OperationSegment");
        }
        public static Db.OeBoundFunctionParameter CreateBoundFunctionParameter(OeQueryContext queryContext)
        {
            var expressionBuilder = queryContext.CreateExpressionBuilder();

            Type sourceEntityType = queryContext.EntitySetAdapter.EntityType;
            IEdmEntitySet sourceEntitySet = OeEdmClrHelper.GetEntitySet(queryContext.EdmModel, queryContext.EntitySetAdapter.EntitySetName);
            Expression source = OeEnumerableStub.CreateEnumerableStubExpression(sourceEntityType, sourceEntitySet);
            source = expressionBuilder.ApplyNavigation(source, queryContext.ParseNavigationSegments);

            IEdmEntitySet targetEntitySet = OeOperationHelper.GetEntitySet(queryContext.ODataUri.Path);
            if (sourceEntitySet != targetEntitySet)
                queryContext.ODataUri.Path = new ODataPath(new EntitySetSegment(targetEntitySet));

            expressionBuilder = queryContext.CreateExpressionBuilder();
            Type targetEntityType = queryContext.EdmModel.GetClrType(targetEntitySet.EntityType());
            Expression target = OeEnumerableStub.CreateEnumerableStubExpression(targetEntityType, targetEntitySet);
            target = expressionBuilder.ApplySelect(target, queryContext);

            OeEntryFactory targetEntryFactory = expressionBuilder.CreateEntryFactory(targetEntitySet, null);
            var sourceQueryExpression = new OeQueryExpression(queryContext.EdmModel, sourceEntitySet, source);
            var targetQueryExpression = new OeQueryExpression(queryContext.EdmModel, targetEntitySet, target, targetEntryFactory);

            Type boundFunctionParameterType = typeof(Db.OeBoundFunctionParameter<,>).MakeGenericType(new[] { sourceEntityType, targetEntityType });
            ConstructorInfo ctor = boundFunctionParameterType.GetConstructors()[0];
            return (Db.OeBoundFunctionParameter)ctor.Invoke(new Object[] { sourceQueryExpression, targetQueryExpression });
        }
        private static void FillParameters(IEdmModel edmModel, List<KeyValuePair<String, Object?>> parameters, Stream requestStream, IEdmOperation operation, String? contentType)
        {
            if (!operation.Parameters.Any())
                return;

            IODataRequestMessage requestMessage = new Infrastructure.OeInMemoryMessage(requestStream, contentType);
            var settings = new ODataMessageReaderSettings() { EnableMessageStreamDisposal = false };
            using (var messageReader = new ODataMessageReader(requestMessage, settings, edmModel))
            {
                ODataParameterReader parameterReader = messageReader.CreateODataParameterReader(operation);
                while (parameterReader.Read())
                {
                    Object? value;
                    switch (parameterReader.State)
                    {
                        case ODataParameterReaderState.Value:
                            {
                                value = OeEdmClrHelper.GetValue(edmModel, parameterReader.Value);
                                break;
                            }
                        case ODataParameterReaderState.Collection:
                            {
                                ODataCollectionReader collectionReader = parameterReader.CreateCollectionReader();
                                value = OeEdmClrHelper.GetValue(edmModel, ReadCollection(collectionReader));
                                break;
                            }
                        case ODataParameterReaderState.Resource:
                            {
                                ODataReader reader = parameterReader.CreateResourceReader();
                                value = OeEdmClrHelper.GetValue(edmModel, ReadResource(reader));
                                break;
                            }
                        case ODataParameterReaderState.ResourceSet:
                            {
                                ODataReader reader = parameterReader.CreateResourceSetReader();
                                value = OeEdmClrHelper.GetValue(edmModel, ReadResourceSet(reader));
                                break;
                            }
                        default:
                            continue;
                    }

                    parameters.Add(new KeyValuePair<String, Object?>(parameterReader.Name, value));
                }
            }
        }
        public static IEdmEntitySet? GetEntitySet(IEdmOperationImport operationImport)
        {
            if (operationImport.Operation.ReturnType is IEdmCollectionTypeReference collectionTypeReference)
            {
                IEdmType edmType = collectionTypeReference.Definition.AsElementType();
                foreach (IEdmEntitySet entitySet in operationImport.Container.EntitySets())
                    if (entitySet.EntityType() == edmType)
                        return entitySet;
            }

            return null;
        }
        public static IEdmEntitySet GetEntitySet(ODataPath path)
        {
            var operationSegment = (OperationSegment)path.LastSegment;
            IEdmOperation edmOperation = operationSegment.Operations.First();
            IEdmType edmEntityType = ((IEdmCollectionType)edmOperation.ReturnType.Definition).ElementType.Definition;

            IEdmEntityContainer container = ((EntitySetSegment)path.FirstSegment).EntitySet.Container;
            foreach (IEdmEntitySet entitySet in container.EntitySets())
                if (entitySet.EntityType() == edmEntityType)
                    return entitySet;

            throw new InvalidOperationException("EntitySet not found for operation " + edmOperation.FullName());
        }
        public static IReadOnlyList<KeyValuePair<String, Object?>> GetParameters(IEdmModel edmModel, ODataPathSegment segment,
            IDictionary<string, SingleValueNode> parameterAliasNodes, Stream? requestStream = null, String? contentType = null)
        {
            var parameters = new List<KeyValuePair<String, Object?>>();

            IEdmOperation operation;
            IEnumerable<OperationSegmentParameter> segmentParameters;
            if (segment is OperationSegment operationSegment)
            {
                segmentParameters = operationSegment.Parameters;
                operation = operationSegment.Operations.Single();
            }
            else if (segment is OperationImportSegment operationImportSegment)
            {
                segmentParameters = operationImportSegment.Parameters;
                operation = (EdmOperation)operationImportSegment.OperationImports.Single().Operation;
            }
            else
                throw new InvalidOperationException("Not supported segment type " + segment.GetType().Name);

            foreach (OperationSegmentParameter segmentParameter in segmentParameters)
            {
                Object? value;
                if (segmentParameter.Value is ConstantNode constantNode)
                    value = OeEdmClrHelper.GetValue(edmModel, constantNode.Value);
                else if (segmentParameter.Value is ParameterAliasNode parameterAliasNode)
                    value = OeEdmClrHelper.GetValue(edmModel, ((ConstantNode)parameterAliasNodes[parameterAliasNode.Alias]).Value);
                else
                    value = OeEdmClrHelper.GetValue(edmModel, segmentParameter.Value);

                parameters.Add(new KeyValuePair<String, Object?>(segmentParameter.Name, value));
            }

            if (parameters.Count == 0 && requestStream != null)
                FillParameters(edmModel, parameters, requestStream, operation, contentType);
            OrderParameters(operation.Parameters, parameters);

            return parameters;
        }
        private static void OrderParameters(IEnumerable<IEdmOperationParameter> operationParameters, List<KeyValuePair<String, Object?>> parameters)
        {
            int pos = 0;
            foreach (IEdmOperationParameter operationParameter in operationParameters)
            {
                for (int i = pos; i < parameters.Count; i++)
                    if (String.Compare(operationParameter.Name, parameters[i].Key, StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        if (i != pos)
                        {
                            KeyValuePair<String, Object?> temp = parameters[pos];
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
            while (reader.Read())
                if (reader.State == ODataReaderState.ResourceEnd)
                    return (ODataResource)reader.Item;

            throw new InvalidOperationException("ResourceEnd not found");
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
