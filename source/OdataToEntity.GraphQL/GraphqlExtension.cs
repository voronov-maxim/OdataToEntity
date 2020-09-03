using GraphQLParser.AST;
using System;
using System.Collections.Generic;

namespace OdataToEntity.GraphQL
{
    internal static class GraphqlExtension
    {
        public static String GetName(this INamedNode namedNode)
        {
            return (namedNode.Name ?? throw new InvalidOperationException("Name is null")).Value ?? throw new InvalidOperationException("Name value is null");
        }
        public static List<ASTNode> GetSelections(this GraphQLSelectionSet selectionSet)
        {
            return selectionSet.Selections ?? throw new InvalidOperationException("Selections is null");
        }
        public static GraphQLSelectionSet GetSelectionSet(GraphQLFieldSelection fieldSelection)
        {
            return fieldSelection.SelectionSet ?? throw new InvalidOperationException("SelectionSet is null");
        }
    }
}
