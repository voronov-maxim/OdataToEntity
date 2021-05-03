using System;
using System.Collections.Generic;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;

namespace OdataToEntity.Parsers
{
    public class OeQueryNodeTranslator : QueryNodeVisitor<QueryNode>
    {
        public QueryNode Visit(QueryNode nodeIn)
        {
            return nodeIn.Accept(this);
        }
        private IEnumerable<QueryNode>? VisitParameters(IEnumerable<QueryNode>? parameters)
        {
            if (parameters == null)
                return null;

            List<QueryNode>? parameterList = null;
            int i = 0;
            foreach (QueryNode parameter in parameters)
            {
                QueryNode parameterNode = Visit(parameter);
                if (parameterNode != parameter)
                {
                    if (parameterList == null)
                        parameterList = new List<QueryNode>(parameters);
                    parameterList[i] = parameterNode;
                }
                i++;
            }
            return parameterList ?? parameters;
        }

        public override QueryNode Visit(AllNode nodeIn)
        {
            var body = (SingleValueNode)Visit(nodeIn.Body);
            CollectionNode? source = nodeIn.Source == null ? null : (CollectionNode)Visit(nodeIn.Source);
            if (nodeIn.Body != body || nodeIn.Source != source)
                nodeIn = new AllNode(nodeIn.RangeVariables, nodeIn.CurrentRangeVariable)
                {
                    Body = body,
                    Source = source
                };
            return nodeIn;
        }
        public override QueryNode Visit(AnyNode nodeIn)
        {
            var body = (SingleValueNode)Visit(nodeIn.Body);
            CollectionNode? source = nodeIn.Source == null ? null : (CollectionNode)Visit(nodeIn.Source);
            if (nodeIn.Body != body || nodeIn.Source != source)
                nodeIn = new AnyNode(nodeIn.RangeVariables, nodeIn.CurrentRangeVariable)
                {
                    Body = body,
                    Source = source
                };
            return nodeIn;
        }
        public override QueryNode Visit(BinaryOperatorNode nodeIn)
        {
            var left = (SingleValueNode)Visit(nodeIn.Left);
            var right = (SingleValueNode)Visit(nodeIn.Right);
            if (nodeIn.Left != left || nodeIn.Right != right)
                nodeIn = new BinaryOperatorNode(nodeIn.OperatorKind, left, right);
            return nodeIn;
        }
        public override QueryNode Visit(CountNode nodeIn)
        {
            CollectionNode? source = nodeIn.Source == null ? null : (CollectionNode)Visit(nodeIn.Source);
            if (nodeIn.Source != source)
                nodeIn = new CountNode(source);
            return nodeIn;
        }
        public override QueryNode Visit(CollectionNavigationNode nodeIn)
        {
            SingleResourceNode? source = nodeIn.Source == null ? null : (SingleResourceNode)Visit(nodeIn.Source);
            if (nodeIn.Source != source)
                nodeIn = new CollectionNavigationNode(source, nodeIn.NavigationProperty, nodeIn.BindingPath);
            return nodeIn;
        }
        public override QueryNode Visit(CollectionPropertyAccessNode nodeIn)
        {
            SingleResourceNode? source = nodeIn.Source == null ? null : (SingleResourceNode)Visit(nodeIn.Source);
            if (nodeIn.Source != source)
                nodeIn = new CollectionPropertyAccessNode(source, nodeIn.Property);
            return nodeIn;
        }
        public override QueryNode Visit(CollectionOpenPropertyAccessNode nodeIn)
        {
            SingleResourceNode? source = nodeIn.Source == null ? null : (SingleResourceNode)Visit(nodeIn.Source);
            if (nodeIn.Source != source)
                nodeIn = new CollectionOpenPropertyAccessNode(source, nodeIn.Name);
            return nodeIn;
        }
        public override QueryNode Visit(ConstantNode nodeIn)
        {
            return nodeIn;
        }
        public override QueryNode Visit(CollectionConstantNode nodeIn)
        {
            Object[]? values = null;
            for (int i = 0; i < nodeIn.Collection.Count; i++)
            {
                var constantNode = (ConstantNode)Visit(nodeIn.Collection[i]);
                if (nodeIn.Collection[i] != constantNode)
                {
                    if (values == null)
                    {
                        values = new Object[nodeIn.Collection.Count];
                        for (int j = 0; j < nodeIn.Collection.Count; j++)
                            values[j] = nodeIn.Collection[j].Value;
                    }
                    values[i] = constantNode.Value;
                }
            }

            if (values != null)
                nodeIn = new CollectionConstantNode(values, nodeIn.LiteralText, nodeIn.CollectionType);
            return nodeIn;
        }
        public override QueryNode Visit(ConvertNode nodeIn)
        {
            SingleValueNode? source = nodeIn.Source == null ? null : (SingleValueNode)Visit(nodeIn.Source);
            if (nodeIn.Source != source)
                nodeIn = new ConvertNode(source, nodeIn.TypeReference);
            return nodeIn;
        }
        public override QueryNode Visit(CollectionResourceCastNode nodeIn)
        {
            CollectionResourceNode? source = nodeIn.Source == null ? null : (CollectionResourceNode)Visit(nodeIn.Source);
            if (nodeIn.Source != source)
                nodeIn = new CollectionResourceCastNode(source, (IEdmStructuredType)nodeIn.ItemStructuredType.Definition);
            return nodeIn;
        }
        public override QueryNode Visit(ResourceRangeVariableReferenceNode nodeIn)
        {
            return nodeIn;
        }
        public override QueryNode Visit(NonResourceRangeVariableReferenceNode nodeIn)
        {
            return nodeIn;
        }
        public override QueryNode Visit(SingleResourceCastNode nodeIn)
        {
            SingleResourceNode? source = nodeIn.Source == null ? null : (SingleResourceNode)Visit(nodeIn.Source);
            if (nodeIn.Source != source)
                nodeIn = new SingleResourceCastNode(source, (IEdmStructuredType)nodeIn.StructuredTypeReference.Definition);
            return nodeIn;
        }
        public override QueryNode Visit(SingleNavigationNode nodeIn)
        {
            SingleResourceNode? source = nodeIn.Source == null ? null : (SingleResourceNode)Visit(nodeIn.Source);
            if (nodeIn.Source != source)
                nodeIn = new SingleNavigationNode(source, nodeIn.NavigationProperty, nodeIn.BindingPath);
            return nodeIn;
        }
        public override QueryNode Visit(SingleResourceFunctionCallNode nodeIn)
        {
            QueryNode? source = nodeIn.Source == null ? null : Visit(nodeIn.Source);
            IEnumerable<QueryNode>? parameters = VisitParameters(nodeIn.Parameters);
            if (nodeIn.Source != source || nodeIn.Parameters != parameters)
                nodeIn = new SingleResourceFunctionCallNode(nodeIn.Name, nodeIn.Functions, parameters, nodeIn.StructuredTypeReference, nodeIn.NavigationSource, source);
            return nodeIn;
        }
        public override QueryNode Visit(SingleValueFunctionCallNode nodeIn)
        {
            QueryNode? source = nodeIn.Source == null ? null : Visit(nodeIn.Source);
            IEnumerable<QueryNode>? parameters = VisitParameters(nodeIn.Parameters);
            if (nodeIn.Source != source || nodeIn.Parameters != parameters)
                nodeIn = new SingleValueFunctionCallNode(nodeIn.Name, nodeIn.Functions, parameters, nodeIn.TypeReference, source);
            return nodeIn;
        }
        public override QueryNode Visit(CollectionResourceFunctionCallNode nodeIn)
        {
            QueryNode? source = nodeIn.Source == null ? null : Visit(nodeIn.Source);
            IEnumerable<QueryNode>? parameters = VisitParameters(nodeIn.Parameters);
            if (nodeIn.Source != source || nodeIn.Parameters != parameters)
                nodeIn = new CollectionResourceFunctionCallNode(nodeIn.Name, nodeIn.Functions, parameters, nodeIn.CollectionType, (IEdmEntitySetBase)nodeIn.NavigationSource, source);
            return nodeIn;
        }
        public override QueryNode Visit(CollectionFunctionCallNode nodeIn)
        {
            QueryNode? source = nodeIn.Source == null ? null : Visit(nodeIn.Source);
            IEnumerable<QueryNode>? parameters = VisitParameters(nodeIn.Parameters);
            if (nodeIn.Source != source || nodeIn.Parameters != parameters)
                nodeIn = new CollectionFunctionCallNode(nodeIn.Name, nodeIn.Functions, parameters, nodeIn.CollectionType, source);
            return nodeIn;
        }
        public override QueryNode Visit(SingleValueOpenPropertyAccessNode nodeIn)
        {
            SingleValueNode? source = nodeIn.Source == null ? null : (SingleValueNode)Visit(nodeIn.Source);
            if (nodeIn.Source != source)
                nodeIn = new SingleValueOpenPropertyAccessNode(source, nodeIn.Name);
            return nodeIn;
        }
        public override QueryNode Visit(SingleValuePropertyAccessNode nodeIn)
        {
            SingleValueNode? source = nodeIn.Source == null ? null : (SingleValueNode)Visit(nodeIn.Source);
            if (nodeIn.Source != source)
                nodeIn = new SingleValuePropertyAccessNode(source, nodeIn.Property);
            return nodeIn;
        }
        public override QueryNode Visit(UnaryOperatorNode nodeIn)
        {
            var operand = (SingleValueNode)Visit(nodeIn.Operand);
            return new UnaryOperatorNode(nodeIn.OperatorKind, operand);
        }
        public override QueryNode Visit(NamedFunctionParameterNode nodeIn)
        {
            QueryNode value = Visit(nodeIn.Value);
            if (nodeIn.Value != value)
                nodeIn = new NamedFunctionParameterNode(nodeIn.Name, value);
            return nodeIn;
        }
        public override QueryNode Visit(ParameterAliasNode nodeIn)
        {
            return nodeIn;
        }
        public override QueryNode Visit(SearchTermNode nodeIn)
        {
            return nodeIn;
        }
        public override QueryNode Visit(SingleComplexNode nodeIn)
        {
            SingleResourceNode? source = nodeIn.Source == null ? null : (SingleResourceNode)Visit(nodeIn.Source);
            return new SingleComplexNode(source, nodeIn.Property);
        }
        public override QueryNode Visit(CollectionComplexNode nodeIn)
        {
            SingleResourceNode? source = nodeIn.Source == null ? null : (SingleResourceNode)Visit(nodeIn.Source);
            if (nodeIn.Source != source)
                nodeIn = new CollectionComplexNode(source, nodeIn.Property);
            return nodeIn;
        }
        public override QueryNode Visit(SingleValueCastNode nodeIn)
        {
            SingleValueNode? source = nodeIn.Source == null ? null : (SingleValueNode)Visit(nodeIn.Source);
            if (nodeIn.Source != source)
                nodeIn = new SingleValueCastNode(source, (IEdmPrimitiveType)nodeIn.TypeReference.Definition);
            return nodeIn;
        }
        public override QueryNode Visit(AggregatedCollectionPropertyNode nodeIn)
        {
            CollectionNavigationNode? source = nodeIn.Source == null ? null : (CollectionNavigationNode)Visit(nodeIn.Source);
            if (nodeIn.Source != source)
                nodeIn = new AggregatedCollectionPropertyNode(source, nodeIn.Property);
            return nodeIn;
        }
        public override QueryNode Visit(InNode nodeIn)
        {
            var left = (SingleValueNode)Visit(nodeIn.Left);
            var right = (CollectionNode)Visit(nodeIn.Right);
            return new InNode(left, right);
        }
    }
}