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
    public sealed class OeGetParser
    {
        private readonly Uri _baseUri;
        private readonly IEdmModel _model;
        private readonly Db.OeDataAdapter _dataAdapter;

        public OeGetParser(Uri baseUri, Db.OeDataAdapter dataAdapter, IEdmModel model)
        {
            _baseUri = baseUri;
            _dataAdapter = dataAdapter;
            _model = model;
        }

        private static FilterClause CreateFilterClause(IEdmEntitySet entitySet, IEdmEntityTypeReference entityTypeRef, KeySegment keySegment)
        {
            var range = new ResourceRangeVariable("", entityTypeRef, entitySet);
            var refNode = new ResourceRangeVariableReferenceNode("$it", range);

            var pair = keySegment.Keys.Single();
            var entityType = (IEdmEntityType)keySegment.EdmType;
            IEdmProperty property = entityType.FindProperty(pair.Key);

            var left = new SingleValuePropertyAccessNode(refNode, property);
            var right = new ConstantNode(pair.Value, ODataUriUtils.ConvertToUriLiteral(pair.Value, ODataVersion.V4));

            var node = new BinaryOperatorNode(BinaryOperatorKind.Equal, left, right);
            return new FilterClause(node, range);
        }
        public async Task ExecuteAsync(Uri requestUri, OeRequestHeaders headers, Stream stream, CancellationToken cancellationToken)
        {
            OeParseUriContext parseUriContext = ParseUri(requestUri);
            parseUriContext.Headers = headers;
            parseUriContext.EntitySetAdapter = _dataAdapter.GetEntitySetAdapter(parseUriContext.EntitySet.Name);

            Object dataContext = null;
            try
            {
                dataContext = _dataAdapter.CreateDataContext();

                if (parseUriContext.IsCountSegment)
                {
                    int count = _dataAdapter.ExecuteScalar<int>(dataContext, parseUriContext);
                    byte[] buffer = System.Text.Encoding.UTF8.GetBytes(count.ToString());
                    stream.Write(buffer, 0, buffer.Length);
                }
                else
                {
                    using (Db.OeEntityAsyncEnumerator asyncEnumerator = _dataAdapter.ExecuteEnumerator(dataContext, parseUriContext, cancellationToken))
                        await Writers.OeGetWriter.SerializeAsync(BaseUri, parseUriContext, asyncEnumerator, stream).ConfigureAwait(false);
                }
            }
            finally
            {
                if (dataContext != null)
                    _dataAdapter.CloseDataContext(dataContext);
            }
        }
        public OeParseUriContext ParseUri(Uri requestUri)
        {
            var odataParser = new ODataUriParser(_model, BaseUri, requestUri);
            odataParser.Resolver.EnableCaseInsensitive = true;
            ODataUri odataUri = odataParser.ParseUri();

            List<OeParseNavigationSegment> navigationSegments = null;
            if (odataUri.Path.LastSegment is KeySegment ||
                odataUri.Path.LastSegment is NavigationPropertySegment)
            {
                navigationSegments = new List<OeParseNavigationSegment>();
                ODataPathSegment previousSegment = null;
                foreach (ODataPathSegment segment in odataUri.Path)
                {
                    if (segment is NavigationPropertySegment)
                    {
                        var navigationSegment = segment as NavigationPropertySegment;
                        if (navigationSegment == odataUri.Path.LastSegment)
                            navigationSegments.Add(new OeParseNavigationSegment(navigationSegment, null));
                        else
                            navigationSegments.Add(new OeParseNavigationSegment(navigationSegment, null));
                    }
                    else if (segment is KeySegment)
                    {
                        IEdmEntitySet previousEntitySet;
                        IEdmEntityTypeReference entityTypeRef;

                        var keySegment = segment as KeySegment;
                        NavigationPropertySegment navigationSegment = null;
                        if (previousSegment is EntitySetSegment)
                        {
                            var previousEntitySetSegment = previousSegment as EntitySetSegment;
                            previousEntitySet = previousEntitySetSegment.EntitySet;
                            entityTypeRef = (IEdmEntityTypeReference)((IEdmCollectionType)previousEntitySetSegment.EdmType).ElementType;
                        }
                        else if (previousSegment is NavigationPropertySegment)
                        {
                            navigationSegment = previousSegment as NavigationPropertySegment;
                            previousEntitySet = (IEdmEntitySet)navigationSegment.NavigationSource;
                            entityTypeRef = (IEdmEntityTypeReference)((IEdmCollectionType)navigationSegment.EdmType).ElementType;
                        }
                        else
                            throw new InvalidOperationException("invalid segment");

                        FilterClause keyFilter = CreateFilterClause(previousEntitySet, entityTypeRef, keySegment);
                        navigationSegments.Add(new OeParseNavigationSegment(navigationSegment, keyFilter));
                    }
                    previousSegment = segment;
                }
            }

            var entitySetSegment = (EntitySetSegment)odataUri.Path.FirstSegment;
            IEdmEntitySet entitySet = entitySetSegment.EntitySet;
            bool isCountSegment = odataUri.Path.LastSegment is CountSegment;
            return new OeParseUriContext(_model, odataUri, entitySet, navigationSegments, isCountSegment);
        }

        public Uri BaseUri => _baseUri;
    }
}
