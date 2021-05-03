﻿using Microsoft.OData.UriParser;
using System;
using System.Collections.Generic;

namespace OdataToEntity.Cache.UriCompare
{
    public readonly struct OeQueryNodeComparer
    {
        private readonly OeCacheComparerParameterValues _parameterValues;

        public OeQueryNodeComparer(in OeCacheComparerParameterValues parameterValues)
        {
            _parameterValues = parameterValues;
        }

        public bool Compare(QueryNode node1, QueryNode node2)
        {
            if (node1 == null || node2 == null)
                return node1 == node2;

            if (node1.Kind != node2.Kind)
                return false;

            return node1.Kind switch
            {
                QueryNodeKind.All => Visit((AllNode)node1, (AllNode)node2),
                QueryNodeKind.Any => Visit((AnyNode)node1, (AnyNode)node2),
                QueryNodeKind.BinaryOperator => Visit((BinaryOperatorNode)node1, (BinaryOperatorNode)node2),
                QueryNodeKind.CollectionNavigationNode => Visit((CollectionNavigationNode)node1, (CollectionNavigationNode)node2),
                QueryNodeKind.Constant => Visit((ConstantNode)node1, (ConstantNode)node2),
                QueryNodeKind.Convert => Visit((ConvertNode)node1, (ConvertNode)node2),
                QueryNodeKind.Count => node1 is CountNode ? Visit((CountNode)node1, (CountNode)node2) : Visit((CountVirtualPropertyNode)node1, (CountVirtualPropertyNode)node1),
                QueryNodeKind.In => Visit((InNode)node1, (InNode)node2),
                QueryNodeKind.ResourceRangeVariableReference => Visit((ResourceRangeVariableReferenceNode)node1, (ResourceRangeVariableReferenceNode)node2),
                QueryNodeKind.SingleNavigationNode => Visit((SingleNavigationNode)node1, (SingleNavigationNode)node2),
                QueryNodeKind.SingleValueFunctionCall => Visit((SingleValueFunctionCallNode)node1, (SingleValueFunctionCallNode)node2),
                QueryNodeKind.SingleValueOpenPropertyAccess => Visit((SingleValueOpenPropertyAccessNode)node1, (SingleValueOpenPropertyAccessNode)node2),
                QueryNodeKind.SingleValuePropertyAccess => Visit((SingleValuePropertyAccessNode)node1, (SingleValuePropertyAccessNode)node2),
                _ => throw new NotSupportedException("node kind " + node1.Kind.ToString()),
            };
        }
        public bool Compare(RangeVariable rangeVariable1, RangeVariable rangeVariable2)
        {
            var range1 = (ResourceRangeVariable)rangeVariable1;
            var range2 = (ResourceRangeVariable)rangeVariable2;

            if (range1 == null || range2 == null)
                return range1 == range2;

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

        private bool Visit(AllNode node1, AllNode node2)
        {
            return node1.TypeReference.IsEqual(node2.TypeReference) &&
                Compare(node1.Source, node2.Source) && Compare(node1.Body, node2.Body);
        }
        private bool Visit(AnyNode node1, AnyNode node2)
        {
            return node1.TypeReference.IsEqual(node2.TypeReference) &&
                Compare(node1.Source, node2.Source) && Compare(node1.Body, node2.Body);
        }
        private bool Visit(BinaryOperatorNode node1, BinaryOperatorNode node2)
        {
            return node1.OperatorKind == node2.OperatorKind &&
                node1.TypeReference.IsEqual(node2.TypeReference) &&
                Compare(node1.Left, node2.Left) && Compare(node1.Right, node2.Right);
        }
        private bool Visit(CollectionNavigationNode node1, CollectionNavigationNode node2)
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
        private bool Visit(ConstantNode node1, ConstantNode node2)
        {
            if (node1.Value == null && node2.Value == null)
                return true;
            if (node1.Value == null || node2.Value == null)
                return false;

            _parameterValues.AddParameter(node1, node2);
            return true;
        }
        private bool Visit(ConvertNode node1, ConvertNode node2)
        {
            return node1.TypeReference.IsEqual(node2.TypeReference) && Compare(node1.Source, node2.Source);
        }
        private bool Visit(CountNode node1, CountNode node2)
        {
            return node1.TypeReference.IsEqual(node2.TypeReference) && Compare(node1.Source, node2.Source);
        }
        private static bool Visit(CountVirtualPropertyNode node1, CountVirtualPropertyNode node2)
        {
            return node1.TypeReference.IsEqual(node2.TypeReference);
        }
        private bool Visit(InNode node1, InNode node2)
        {
            var propertyNode1 = (SingleValuePropertyAccessNode)node1.Left;
            var propertyNode2 = (SingleValuePropertyAccessNode)node2.Left;
            if (!Visit(propertyNode1, propertyNode2))
                return false;

            var constantNodes1 = (CollectionConstantNode)node1.Right;
            var constantNodes2 = (CollectionConstantNode)node2.Right;
            if (constantNodes1.Collection.Count != constantNodes2.Collection.Count)
                return false;

            for (int i = 0; i < constantNodes1.Collection.Count; i++)
                if (!Visit(constantNodes1.Collection[i], constantNodes2.Collection[i]))
                    return false;

            return true;
        }
        private bool Visit(ResourceRangeVariableReferenceNode node1, ResourceRangeVariableReferenceNode node2)
        {
            if (node1.Name != node2.Name)
                return false;
            if (node1.NavigationSource != node2.NavigationSource)
                return false;
            if (!Compare(node1.RangeVariable, node2.RangeVariable))
                return false;
            if (!node1.StructuredTypeReference.IsEqual(node2.StructuredTypeReference))
                return false;
            return node1.TypeReference.IsEqual(node2.TypeReference);
        }
        private bool Visit(SingleNavigationNode node1, SingleNavigationNode node2)
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

            return Compare(node1.Source, node2.Source);
        }
        private bool Visit(SingleValueFunctionCallNode node1, SingleValueFunctionCallNode node2)
        {
            if (node1.Name != node2.Name)
                return false;
            if (node1.Source != node2.Source)
                return false;
            if (node1.TypeReference != node2.TypeReference)
                return false;

            IEnumerator<QueryNode>? e1 = null;
            IEnumerator<QueryNode>? e2 = null;
            try
            {
                e1 = node1.Parameters.GetEnumerator();
                e2 = node2.Parameters.GetEnumerator();
                for (; ; )
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
        private bool Visit(SingleValueOpenPropertyAccessNode node1, SingleValueOpenPropertyAccessNode node2)
        {
            return node1.Name == node2.Name &&
                node1.TypeReference.IsEqual(node2.TypeReference) &&
                Compare(node1.Source, node2.Source);
        }
        private bool Visit(SingleValuePropertyAccessNode node1, SingleValuePropertyAccessNode node2)
        {
            return node1.Property == node2.Property &&
                node1.TypeReference.IsEqual(node2.TypeReference) &&
                Compare(node1.Source, node2.Source);
        }
    }

}
