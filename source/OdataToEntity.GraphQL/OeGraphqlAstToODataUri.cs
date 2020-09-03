using GraphQL;
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
    public readonly struct OeGraphqlAstToODataUri
    {
        private sealed class ChangeNavigationPathVisitor : QueryNodeVisitor<SingleValueNode>
        {
            private readonly SingleNavigationNode _singleNavigationNode;

            public ChangeNavigationPathVisitor(SingleResourceNode parent, IEdmNavigationProperty navigationProperty)
            {
                _singleNavigationNode = new SingleNavigationNode(parent, navigationProperty, null);
            }

            public override SingleValueNode Visit(BinaryOperatorNode nodeIn)
            {
                SingleValueNode left = Visit((SingleValuePropertyAccessNode)nodeIn.Left);
                return new BinaryOperatorNode(nodeIn.OperatorKind, left, nodeIn.Right);
            }
            public override SingleValueNode Visit(SingleValuePropertyAccessNode nodeIn)
            {
                return new SingleValuePropertyAccessNode(_singleNavigationNode, nodeIn.Property);
            }
        }

        private readonly IResolveFieldContext _context;
        private readonly IEdmModel _edmModel;

        public OeGraphqlAstToODataUri(IEdmModel edmModel, IResolveFieldContext context)
        {
            _edmModel = edmModel;
            _context = context;
        }

        private FilterClause? BuildFilterClause(IEdmEntitySet entitySet, GraphQLFieldSelection selection)
        {
            ResourceRangeVariable resourceVariable = GetResorceVariable(entitySet);
            var resourceNode = new ResourceRangeVariableReferenceNode("", resourceVariable);
            BinaryOperatorNode? filterExpression = BuildFilterExpression(resourceNode, selection);
            if (filterExpression == null)
                return null;

            return new FilterClause(filterExpression, resourceVariable);
        }
        private BinaryOperatorNode? BuildFilterExpression(SingleResourceNode source, GraphQLFieldSelection selection)
        {
            BinaryOperatorNode? compositeNode = null;
            IEdmEntityType entityType = source.NavigationSource.EntityType();

            if (selection.Arguments != null)
                foreach (GraphQLArgument argument in selection.Arguments)
                {
                    IEdmProperty edmProperty = FindEdmProperty(entityType, argument.GetName());
                    var left = new SingleValuePropertyAccessNode(source, edmProperty);

                    Object? value = GetArgumentValue(edmProperty.Type, argument.Value);
                    var right = new ConstantNode(value, ODataUriUtils.ConvertToUriLiteral(value, ODataVersion.V4));
                    var node = new BinaryOperatorNode(BinaryOperatorKind.Equal, left, right);
                    compositeNode = ComposeExpression(compositeNode, node);
                }

            return compositeNode;
        }
        private SelectExpandClause BuildSelectExpandClause(IEdmEntitySet entitySet, GraphQLSelectionSet selectionSet)
        {
            var selectItems = new List<SelectItem>();
            foreach (ASTNode astNode in selectionSet.GetSelections())
                if (astNode is GraphQLFieldSelection fieldSelection)
                {
                    IEdmProperty edmProperty = FindEdmProperty(entitySet.EntityType(), fieldSelection.GetName());
                    if (fieldSelection.SelectionSet == null)
                    {
                        var structuralProperty = (IEdmStructuralProperty)edmProperty;
                        selectItems.Add(new PathSelectItem(new ODataSelectPath(new PropertySegment(structuralProperty))));
                    }
                    else
                    {
                        var navigationProperty = (IEdmNavigationProperty)edmProperty;
                        IEdmEntitySet parentEntitySet;
                        if (navigationProperty.ContainsTarget)
                        {
                            ModelBuilder.ManyToManyJoinDescription joinDescription = _edmModel.GetManyToManyJoinDescription(navigationProperty);
                            parentEntitySet = OeEdmClrHelper.GetEntitySet(_edmModel, joinDescription.TargetNavigationProperty);
                        }
                        else
                            parentEntitySet = OeEdmClrHelper.GetEntitySet(_edmModel, navigationProperty);

                        var expandPath = new ODataExpandPath(new NavigationPropertySegment(navigationProperty, parentEntitySet));

                        FilterClause? filterOption = null;
                        if (fieldSelection.Arguments != null && fieldSelection.Arguments.Any())
                            filterOption = BuildFilterClause(parentEntitySet, fieldSelection);

                        SelectExpandClause childSelectExpand = BuildSelectExpandClause(parentEntitySet, fieldSelection.SelectionSet);
                        var expandedSelectItem = new ExpandedNavigationSelectItem(expandPath, parentEntitySet, childSelectExpand, filterOption, null, null, null, null, null, null);
                        selectItems.Add(expandedSelectItem);
                    }
                }
                else
                    throw new NotSupportedException("selection " + astNode.GetType().Name + " not supported");

            return new SelectExpandClause(selectItems, false);
        }
        private static BinaryOperatorNode ComposeExpression(BinaryOperatorNode? left, BinaryOperatorNode right)
        {
            if (left == null)
                return right;

            return new BinaryOperatorNode(BinaryOperatorKind.And, left, right);
        }
        private static IEdmProperty FindEdmProperty(IEdmStructuredType edmType, String name)
        {
            foreach (IEdmProperty edmProperty in edmType.Properties())
                if (String.Compare(edmProperty.Name, name, StringComparison.OrdinalIgnoreCase) == 0)
                    return edmProperty;

            throw new InvalidOperationException("Property " + name + " not found in edm type " + edmType.FullTypeName());
        }
        private Object? GetArgumentValue(IEdmTypeReference typeReference, GraphQLValue? graphValue)
        {
            if (graphValue is GraphQLScalarValue scalarValue)
            {
                if (typeReference.IsString())
                    return scalarValue.Value;

                return ODataUriUtils.ConvertFromUriLiteral(scalarValue.Value, ODataVersion.V4, _edmModel, typeReference);
            }
            else if (graphValue is GraphQLVariable variable)
                return _context.Arguments[variable.GetName()];
            else if (graphValue is null)
                return null;

            throw new NotSupportedException("Argument " + graphValue.GetType().Name + " not supported");
        }
        private static GraphQLFieldSelection GetSelection(GraphQLDocument document)
        {
            var operationDefinition = (document.Definitions ?? throw new InvalidOperationException("Definitions is null")).OfType<GraphQLOperationDefinition>().Single();
            return (operationDefinition.SelectionSet ?? throw new InvalidOperationException("SelectionSet is null")).GetSelections().OfType<GraphQLFieldSelection>().Single();
        }
        private IEdmEntitySet GetEntitySet(GraphQLFieldSelection selection)
        {
            foreach (FieldType fieldType in _context.Schema.Query.Fields)
                if (String.Compare(fieldType.Name, selection.GetName(), StringComparison.OrdinalIgnoreCase) == 0)
                    return OeEdmClrHelper.GetEntitySet(_edmModel, fieldType.Name);

            throw new InvalidOperationException("Field name " + selection.GetName() + " not found in schema query");
        }
        private static ResourceRangeVariable GetResorceVariable(IEdmEntitySet entitySet)
        {
            var entityTypeRef = (IEdmEntityTypeReference)((IEdmCollectionType)entitySet.Type).ElementType;
            return new ResourceRangeVariable("", entityTypeRef, entitySet);
        }
        private static SelectExpandClause LiftRequiredSingleNavigationPropertyFilter(IEdmEntitySet entitySet,
            SelectExpandClause selectExpandClause, ref FilterClause? filter)
        {
            bool changed = false;

            var selectedItems = selectExpandClause.SelectedItems.ToList();
            for (int i = 0; i < selectedItems.Count; i++)
                if (selectedItems[i] is ExpandedNavigationSelectItem expandedNavigation)
                {
                    FilterClause? childFilter = expandedNavigation.FilterOption;
                    SelectExpandClause childSelectExpand = LiftRequiredSingleNavigationPropertyFilter(
                        (IEdmEntitySet)expandedNavigation.NavigationSource, expandedNavigation.SelectAndExpand, ref childFilter);
                    if (childSelectExpand != expandedNavigation.SelectAndExpand)
                    {
                        changed = true;
                        selectedItems[i] = new ExpandedNavigationSelectItem(
                            expandedNavigation.PathToNavigationProperty,
                            expandedNavigation.NavigationSource,
                            childSelectExpand,
                            childFilter,
                            null, null, null, null, null, null);
                    }

                    foreach (ODataPathSegment pathSegment in expandedNavigation.PathToNavigationProperty)
                        if (pathSegment is NavigationPropertySegment navigationPropertySegment)
                        {
                            IEdmTypeReference edmTypeRef = navigationPropertySegment.NavigationProperty.Type;
                            if (childFilter != null && !edmTypeRef.IsNullable && !edmTypeRef.IsCollection())
                            {
                                changed = true;
                                filter = MergeFilterClause(entitySet, navigationPropertySegment.NavigationProperty, childFilter, filter);

                                selectedItems[i] = new ExpandedNavigationSelectItem(
                                    expandedNavigation.PathToNavigationProperty,
                                    expandedNavigation.NavigationSource,
                                    childSelectExpand);
                                break;
                            }
                        }
                }

            return changed ? new SelectExpandClause(selectedItems, selectExpandClause.AllSelected) : selectExpandClause;
        }
        private static FilterClause MergeFilterClause(IEdmEntitySet entitySet, IEdmNavigationProperty navigationProperty, FilterClause source, FilterClause? target)
        {
            ResourceRangeVariable resourceVariable = GetResorceVariable(entitySet);
            ResourceRangeVariableReferenceNode resourceNode = new ResourceRangeVariableReferenceNode("", resourceVariable);
            ChangeNavigationPathVisitor visitor = new ChangeNavigationPathVisitor(resourceNode, navigationProperty);
            SingleValueNode expression = visitor.Visit((BinaryOperatorNode)source.Expression);

            if (target != null)
                expression = new BinaryOperatorNode(BinaryOperatorKind.And, target.Expression, expression);

            return new FilterClause(expression, resourceVariable);
        }
        public ODataUri Translate(String query)
        {
            var parser = new Parser(new Lexer());
            GraphQLDocument document = parser.Parse(new Source(query));

            GraphQLFieldSelection selection = GetSelection(document);
            IEdmEntitySet entitySet = GetEntitySet(selection);
            SelectExpandClause selectExpandClause = BuildSelectExpandClause(entitySet, selection.SelectionSet ?? throw new InvalidOperationException("SelectionSet is null"));
            FilterClause? filterClause = BuildFilterClause(entitySet, selection);
            selectExpandClause = LiftRequiredSingleNavigationPropertyFilter(entitySet, selectExpandClause, ref filterClause);

            return new ODataUri()
            {
                Path = new ODataPath(new EntitySetSegment(entitySet)),
                SelectAndExpand = selectExpandClause,
                Filter = filterClause
            };
        }
    }
}

