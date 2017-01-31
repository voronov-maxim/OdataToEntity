using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OdataToEntity.Parsers.UriCompare
{
    public static class OeQueryNodeComparer
    {
        public static bool Compare(QueryNode node1, QueryNode node2)
        {
            if (node1 == node2)
                return true;
            if (node1 == null || node2 == null)
                return false;

            if (node1.Kind != node2.Kind)
                return false;

            switch (node1.Kind)
            {
                case QueryNodeKind.All:
                    return Visit((AllNode)node1, (AllNode)node2);
                case QueryNodeKind.Any:
                    return Visit((AnyNode)node1, (AnyNode)node2);
                case QueryNodeKind.BinaryOperator:
                    return Visit((BinaryOperatorNode)node1, (BinaryOperatorNode)node2);
                case QueryNodeKind.CollectionNavigationNode:
                    return Visit((CollectionNavigationNode)node1, (CollectionNavigationNode)node2);
                case QueryNodeKind.Constant:
                    return Visit((ConstantNode)node1, (ConstantNode)node2);
                case QueryNodeKind.Convert:
                    return Visit((ConvertNode)node1, (ConvertNode)node2);
                case QueryNodeKind.Count:
                    return Visit((CountNode)node1, (CountNode)node2);
                case QueryNodeKind.ResourceRangeVariableReference:
                    return Visit((ResourceRangeVariableReferenceNode)node1, (ResourceRangeVariableReferenceNode)node2);
                case QueryNodeKind.SingleNavigationNode:
                    return Visit((SingleNavigationNode)node1, (SingleNavigationNode)node2);
                case QueryNodeKind.SingleValueFunctionCall:
                    return Visit((SingleValueFunctionCallNode)node1, (SingleValueFunctionCallNode)node2);
                case QueryNodeKind.SingleValueOpenPropertyAccess:
                    return Visit((SingleValueOpenPropertyAccessNode)node1, (SingleValueOpenPropertyAccessNode)node2);
                case QueryNodeKind.SingleValuePropertyAccess:
                    return Visit((SingleValuePropertyAccessNode)node1, (SingleValuePropertyAccessNode)node2);
            }

            throw new NotSupportedException("node kind " + node1.Kind.ToString());
        }
        public static bool IsEqual(this IEdmTypeReference @this, IEdmTypeReference edmTypeReference)
        {
            if (@this == edmTypeReference)
                return true;
            if (@this == null || edmTypeReference == null)
                return false;

            return @this.Definition == edmTypeReference.Definition && @this.IsNullable == edmTypeReference.IsNullable;
        }
        public static bool IsEqual(this RangeVariable @this, RangeVariable rangeVariable)
        {
            var range1 = (ResourceRangeVariable)@this;
            var range2 = (ResourceRangeVariable)rangeVariable;

            if (range1 == range2)
                return true;
            if (range1 == null || range2 == null)
                return false;

            if (range1.Kind != range2.Kind)
                return false;
            if (range1.Name != range2.Name)
                return false;
            if (range1.NavigationSource != range2.NavigationSource)
                return false;
            if (!range1.StructuredTypeReference.IsEqual(range2.StructuredTypeReference))
                return false;
            if (!range1.TypeReference.IsEqual(range2.TypeReference))
                return false;

            return Compare(range1.CollectionResourceNode, range2.CollectionResourceNode);
        }

        private static bool Visit(AllNode node1, AllNode node2)
        {
            return node1.TypeReference.IsEqual(node2.TypeReference) &&
                Compare(node1.Source, node2.Source) && Compare(node1.Body, node2.Body);
        }
        private static bool Visit(AnyNode node1, AnyNode node2)
        {
            return node1.TypeReference.IsEqual(node2.TypeReference) &&
                Compare(node1.Source, node2.Source) && Compare(node1.Body, node2.Body);
        }
        private static bool Visit(BinaryOperatorNode node1, BinaryOperatorNode node2)
        {
            return node1.OperatorKind == node1.OperatorKind &&
                node1.TypeReference.IsEqual(node2.TypeReference) &&
                Compare(node1.Left, node2.Left) && Compare(node1.Right, node2.Right);
        }
        private static bool Visit(CollectionNavigationNode node1, CollectionNavigationNode node2)
        {
            if (node1.BindingPath != node2.BindingPath)
                return false;
            if (node1.CollectionType != node2.CollectionType)
                return false;
            if (node1.EntityItemType != node2.EntityItemType)
                return false;
            if (node1.ItemStructuredType != node2.ItemStructuredType)
                return false;
            if (node1.ItemType != node2.ItemType)
                return false;
            if (node1.NavigationProperty != node2.NavigationProperty)
                return false;
            if (node1.NavigationSource != node2.NavigationSource)
                return false;
            if (node1.TargetMultiplicity != node2.TargetMultiplicity)
                return false;

            return Compare(node1.Source, node2.Source);
        }
        private static bool Visit(ConstantNode node1, ConstantNode node2)
        {
            return true;
        }
        private static bool Visit(ConvertNode node1, ConvertNode node2)
        {
            return node1.TypeReference.IsEqual(node2.TypeReference) && Compare(node1.Source, node2.Source);
        }
        private static bool Visit(CountNode node1, CountNode node2)
        {
            return node1.TypeReference.IsEqual(node2.TypeReference) && Compare(node1.Source, node2.Source);
        }
        private static bool Visit(ResourceRangeVariableReferenceNode node1, ResourceRangeVariableReferenceNode node2)
        {
            if (node1.Name != node2.Name)
                return false;
            if (node1.NavigationSource != node2.NavigationSource)
                return false;
            if (!node1.RangeVariable.IsEqual(node2.RangeVariable))
                return false;
            if (!node1.StructuredTypeReference.IsEqual(node2.StructuredTypeReference))
                return false;
            return node1.TypeReference.IsEqual(node2.TypeReference);
        }
        private static bool Visit(SingleNavigationNode node1, SingleNavigationNode node2)
        {
            if (node1.BindingPath != node2.BindingPath)
                return false;
            if (node1.EntityTypeReference != node2.EntityTypeReference)
                return false;
            if (node1.TypeReference != node2.TypeReference)
                return false;
            if (node1.NavigationProperty != node2.NavigationProperty)
                return false;
            if (node1.NavigationSource != node2.NavigationSource)
                return false;
            if (node1.StructuredTypeReference != node2.StructuredTypeReference)
                return false;
            if (node1.TargetMultiplicity != node2.TargetMultiplicity)
                return false;
            if (node1.TypeReference != node2.TypeReference)
                return false;

            return Compare(node1.Source, node2.Source);
        }
        private static bool Visit(SingleValueFunctionCallNode node1, SingleValueFunctionCallNode node2)
        {
            if (node1.Name != node2.Name)
                return false;
            if (node1.Source != node2.Source)
                return false;
            if (node1.TypeReference != node2.TypeReference)
                return false;

            IEnumerator<QueryNode> e1 = null;
            IEnumerator<QueryNode> e2 = null;
            try
            {
                e1 = node1.Parameters.GetEnumerator();
                e2 = node1.Parameters.GetEnumerator();
                for (;;)
                {
                    bool f1 = e1.MoveNext();
                    bool f2 = e2.MoveNext();
                    if (f1 != f2)
                        return false;
                    if (!f1)
                        break;

                    if (!Compare(e1.Current, e2.Current))
                        return false;
                }
            }
            finally
            {
                if (e1 != null)
                    e1.Dispose();
                if (e2 != null)
                    e2.Dispose();
            }

            return true;
        }
        private static bool Visit(SingleValueOpenPropertyAccessNode node1, SingleValueOpenPropertyAccessNode node2)
        {
            return node1.Name == node2.Name &&
                node1.TypeReference.IsEqual(node2.TypeReference) &&
                Compare(node1.Source, node2.Source);
        }
        private static bool Visit(SingleValuePropertyAccessNode node1, SingleValuePropertyAccessNode node2)
        {
            return node1.Property == node2.Property &&
                node1.TypeReference.IsEqual(node2.TypeReference) &&
                Compare(node1.Source, node2.Source);
        }
    }
}
