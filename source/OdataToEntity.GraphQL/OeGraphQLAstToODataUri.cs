using GraphQL.Types;
using GraphQLParser;
using GraphQLParser.AST;
using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OdataToEntity.GraphQL
{
    public readonly struct OeGraphQLAstToODataUri
    {
        private readonly ResolveFieldContext _context;
        private readonly IEdmModel _edmModel;

        public OeGraphQLAstToODataUri(IEdmModel edmModel, ResolveFieldContext context)
        {
            _edmModel = edmModel;
            _context = context;
        }

        private FilterClause BuildFilterClause(IEdmEntitySet entitySet, GraphQLFieldSelection selection)
        {
            ResourceRangeVariable resourceVariable = GetResorceVariable(entitySet);
            var resourceNode = new ResourceRangeVariableReferenceNode("", resourceVariable);
            BinaryOperatorNode filterExpression = BuildFilterExpression(resourceNode, selection);
            if (filterExpression == null)
                return null;

            return new FilterClause(filterExpression, resourceVariable);
        }
        private BinaryOperatorNode BuildFilterExpression(SingleResourceNode source, GraphQLFieldSelection selection)
        {
            BinaryOperatorNode compositeNode = null;
            IEdmEntityType entityType = source.NavigationSource.EntityType();

            foreach (GraphQLArgument argument in selection.Arguments)
            {
                IEdmProperty edmProperty = FindEdmProperty(entityType, argument.Name.Value);
                var left = new SingleValuePropertyAccessNode(source, edmProperty);

                Object value = GetArgumentValue(edmProperty.Type, argument.Value);
                var right = new ConstantNode(value, ODataUriUtils.ConvertToUriLiteral(value, ODataVersion.V4));
                var node = new BinaryOperatorNode(BinaryOperatorKind.Equal, left, right);
                compositeNode = ComposeExpression(compositeNode, node);
            }

            //foreach (ASTNode astNode in selection.SelectionSet.Selections)
            //    if (astNode is GraphQLFieldSelection fieldSelection && fieldSelection.SelectionSet != null)
            //    {
            //        var navigationProperty = (IEdmNavigationProperty)FindEdmProperty(entityType, fieldSelection.Name.Value);
            //        if (navigationProperty.Type is IEdmCollectionTypeReference)
            //            continue;

            //        var parentSource = new SingleNavigationNode(source, navigationProperty, null);
            //        BinaryOperatorNode node = BuildFilterExpression(parentSource, fieldSelection);
            //        compositeNode = ComposeExpression(compositeNode, node);
            //    }

            return compositeNode;
        }
        private SelectExpandClause BuildSelectExpandClause(IEdmEntitySet entitySet, GraphQLSelectionSet selectionSet)
        {
            var selectItems = new List<SelectItem>();
            foreach (ASTNode astNode in selectionSet.Selections)
                if (astNode is GraphQLFieldSelection fieldSelection)
                {
                    IEdmProperty edmProperty = FindEdmProperty(entitySet.EntityType(), fieldSelection.Name.Value);
                    if (fieldSelection.SelectionSet == null)
                    {
                        var structuralProperty = (IEdmStructuralProperty)edmProperty;
                        selectItems.Add(new PathSelectItem(new ODataSelectPath(new PropertySegment(structuralProperty))));
                    }
                    else
                    {
                        IEdmType edmType = edmProperty.Type.Definition;
                        if (edmType is IEdmCollectionType collectionType)
                            edmType = collectionType.ElementType.Definition;

                        IEdmEntitySet parentEntitySet = OeEdmClrHelper.GetEntitySet(_edmModel, edmType);
                        SelectExpandClause childSelectExpand = BuildSelectExpandClause(parentEntitySet, fieldSelection.SelectionSet);

                        var navigationProperty = (IEdmNavigationProperty)edmProperty;
                        IEdmEntitySet navigationSource = OeEdmClrHelper.GetEntitySet(_edmModel, navigationProperty.ToEntityType());
                        var expandPath = new ODataExpandPath(new NavigationPropertySegment(navigationProperty, navigationSource));

                        FilterClause filterOption = null;
                        if (fieldSelection.Arguments.Any())
                            filterOption = BuildFilterClause(parentEntitySet, fieldSelection);

                        var expandedSelectItem = new ExpandedNavigationSelectItem(expandPath, navigationSource, childSelectExpand, filterOption, null, null, null, null, null, null);
                        selectItems.Add(expandedSelectItem);
                    }
                }
                else
                    throw new NotSupportedException("selection " + astNode.GetType().Name + " not supported");

            return new SelectExpandClause(selectItems, false);
        }
        private static BinaryOperatorNode ComposeExpression(BinaryOperatorNode left, BinaryOperatorNode right)
        {
            if (left == null)
                return right;

            return new BinaryOperatorNode(BinaryOperatorKind.And, left, right);
        }
        private static IEdmProperty FindEdmProperty(IEdmStructuredType edmType, String name)
        {
            return edmType.Properties().Single(p => String.Compare(p.Name, name, StringComparison.OrdinalIgnoreCase) == 0);
        }
        private Object GetArgumentValue(IEdmTypeReference typeReference, GraphQLValue graphValue)
        {
            if (graphValue is GraphQLScalarValue scalarValue)
            {
                if (typeReference.IsString())
                    return scalarValue.Value;

                return ODataUriUtils.ConvertFromUriLiteral(scalarValue.Value, ODataVersion.V4, _edmModel, typeReference);
            }
            else if (graphValue is GraphQLVariable variable)
            {
                Type clrType = OeEdmClrHelper.GetClrType(_edmModel, typeReference.Definition);
                return _context.GetArgument(clrType, variable.Name.Value);
            }

            throw new NotSupportedException("argument " + graphValue.GetType().Name + " not supported");
        }
        private static GraphQLFieldSelection GetSelection(GraphQLDocument document)
        {
            var operationDefinition = document.Definitions.OfType<GraphQLOperationDefinition>().Single();
            return operationDefinition.SelectionSet.Selections.OfType<GraphQLFieldSelection>().Single();
        }
        private IEdmEntitySet GetEntitySet(GraphQLFieldSelection selection)
        {
            var listGraphType = (ListGraphType)Schema.Query.Fields.Single(f => f.Name == selection.Name.Value).ResolvedType;
            Type entityType = listGraphType.ResolvedType.GetType().GetGenericArguments()[0];

            return OeEdmClrHelper.GetEntitySet(_edmModel, entityType);
        }
        private static ResourceRangeVariable GetResorceVariable(IEdmEntitySet entitySet)
        {
            var entityTypeRef = (IEdmEntityTypeReference)((IEdmCollectionType)entitySet.Type).ElementType;
            return new ResourceRangeVariable("", entityTypeRef, entitySet);
        }
        public ODataUri Translate(String query)
        {
            var parser = new Parser(new Lexer());
            GraphQLDocument document = parser.Parse(new Source(query));

            GraphQLFieldSelection selection = GetSelection(document);
            IEdmEntitySet entitySet = GetEntitySet(selection);
            return new ODataUri()
            {
                Path = new ODataPath(new EntitySetSegment(entitySet)),
                SelectAndExpand = BuildSelectExpandClause(entitySet, selection.SelectionSet),
                Filter = BuildFilterClause(entitySet, selection)
            };
        }

        private ISchema Schema => _context.Schema;
    }
}

