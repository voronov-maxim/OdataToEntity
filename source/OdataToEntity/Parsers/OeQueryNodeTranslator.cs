using System;
using System.Collections.Generic;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;

namespace OdataToEntity.Parsers
{
    public class OeQueryNodeTranslator : QueryNodeVisitor<QueryNode>
    {
        public QueryNode Visit(QueryNode node)
        {
            return node.Accept(this);
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

        public override QueryNode Visit(AllNode node)
        {
            var body = (SingleValueNode)Visit(node.Body);
            CollectionNode? source = node.Source == null ? null : (CollectionNode)Visit(node.Source);
            if (node.Body != body || node.Source != source)
                node = new AllNode(node.RangeVariables, node.CurrentRangeVariable)
                {
                    Body = body,
                    Source = source
                };
            return node;
        }
        public override QueryNode Visit(AnyNode node)
        {
            var body = (SingleValueNode)Visit(node.Body);
            CollectionNode? source = node.Source == null ? null : (CollectionNode)Visit(node.Source);
            if (node.Body != body || node.Source != source)
                node = new AnyNode(node.RangeVariables, node.CurrentRangeVariable)
                {
                    Body = body,
                    Source = source
                };
            return node;
        }
        public override QueryNode Visit(BinaryOperatorNode node)
        {
            var left = (SingleValueNode)Visit(node.Left);
            var right = (SingleValueNode)Visit(node.Right);
            if (node.Left != left || node.Right != right)
                node = new BinaryOperatorNode(node.OperatorKind, left, right);
            return node;
        }
        public override QueryNode Visit(CountNode node)
        {
            CollectionNode? source = node.Source == null ? null : (CollectionNode)Visit(node.Source);
            if (node.Source != source)
                node = new CountNode(source);
            return node;
        }
        public override QueryNode Visit(CollectionNavigationNode node)
        {
            SingleResourceNode? source = node.Source == null ? null : (SingleResourceNode)Visit(node.Source);
            if (node.Source != source)
                node = new CollectionNavigationNode(source, node.NavigationProperty, node.BindingPath);
            return node;
        }
        public override QueryNode Visit(CollectionPropertyAccessNode node)
        {
            SingleResourceNode? source = node.Source == null ? null : (SingleResourceNode)Visit(node.Source);
            if (node.Source != source)
                node = new CollectionPropertyAccessNode(source, node.Property);
            return node;
        }
        public override QueryNode Visit(CollectionOpenPropertyAccessNode node)
        {
            SingleResourceNode? source = node.Source == null ? null : (SingleResourceNode)Visit(node.Source);
            if (node.Source != source)
                node = new CollectionOpenPropertyAccessNode(source, node.Name);
            return node;
        }
        public override QueryNode Visit(ConstantNode node)
        {
            return node;
        }
        public override QueryNode Visit(CollectionConstantNode node)
        {
            Object[]? values = null;
            for (int i = 0; i < node.Collection.Count; i++)
            {
                var constantNode = (ConstantNode)Visit(node.Collection[i]);
                if (node.Collection[i] != constantNode)
                {
                    if (values == null)
                    {
                        values = new Object[node.Collection.Count];
                        for (int j = 0; j < node.Collection.Count; j++)
                            values[j] = node.Collection[j].Value;
                    }
                    values[i] = constantNode.Value;
                }
            }

            if (values != null)
                node = new CollectionConstantNode(values, node.LiteralText, node.CollectionType);
            return node;
        }
        public override QueryNode Visit(ConvertNode node)
        {
            SingleValueNode? source = node.Source == null ? null : (SingleValueNode)Visit(node.Source);
            if (node.Source != source)
                node = new ConvertNode(source, node.TypeReference);
            return node;
        }
        public override QueryNode Visit(CollectionResourceCastNode node)
        {
            CollectionResourceNode? source = node.Source == null ? null : (CollectionResourceNode)Visit(node.Source);
            if (node.Source != source)
                node = new CollectionResourceCastNode(source, (IEdmStructuredType)node.ItemStructuredType.Definition);
            return node;
        }
        public override QueryNode Visit(ResourceRangeVariableReferenceNode node)
        {
            return node;
        }
        public override QueryNode Visit(NonResourceRangeVariableReferenceNode node)
        {
            return node;
        }
        public override QueryNode Visit(SingleResourceCastNode node)
        {
            SingleResourceNode? source = node.Source == null ? null : (SingleResourceNode)Visit(node.Source);
            if (node.Source != source)
                node = new SingleResourceCastNode(source, (IEdmStructuredType)node.StructuredTypeReference.Definition);
            return node;
        }
        public override QueryNode Visit(SingleNavigationNode node)
        {
            SingleResourceNode? source = node.Source == null ? null : (SingleResourceNode)Visit(node.Source);
            if (node.Source != source)
                node = new SingleNavigationNode(source, node.NavigationProperty, node.BindingPath);
            return node;
        }
        public override QueryNode Visit(SingleResourceFunctionCallNode node)
        {
            QueryNode? source = node.Source == null ? null : Visit(node.Source);
            IEnumerable<QueryNode>? parameters = VisitParameters(node.Parameters);
            if (node.Source != source || node.Parameters != parameters)
                node = new SingleResourceFunctionCallNode(node.Name, node.Functions, parameters, node.StructuredTypeReference, node.NavigationSource, source);
            return node;
        }
        public override QueryNode Visit(SingleValueFunctionCallNode node)
        {
            QueryNode? source = node.Source == null ? null : Visit(node.Source);
            IEnumerable<QueryNode>? parameters = VisitParameters(node.Parameters);
            if (node.Source != source || node.Parameters != parameters)
                node = new SingleValueFunctionCallNode(node.Name, node.Functions, parameters, node.TypeReference, source);
            return node;
        }
        public override QueryNode Visit(CollectionResourceFunctionCallNode node)
        {
            QueryNode? source = node.Source == null ? null : Visit(node.Source);
            IEnumerable<QueryNode>? parameters = VisitParameters(node.Parameters);
            if (node.Source != source || node.Parameters != parameters)
                node = new CollectionResourceFunctionCallNode(node.Name, node.Functions, parameters, node.CollectionType, (IEdmEntitySetBase)node.NavigationSource, source);
            return node;
        }
        public override QueryNode Visit(CollectionFunctionCallNode node)
        {
            QueryNode? source = node.Source == null ? null : Visit(node.Source);
            IEnumerable<QueryNode>? parameters = VisitParameters(node.Parameters);
            if (node.Source != source || node.Parameters != parameters)
                node = new CollectionFunctionCallNode(node.Name, node.Functions, parameters, node.CollectionType, source);
            return node;
        }
        public override QueryNode Visit(SingleValueOpenPropertyAccessNode node)
        {
            SingleValueNode? source = node.Source == null ? null : (SingleValueNode)Visit(node.Source);
            if (node.Source != source)
                node = new SingleValueOpenPropertyAccessNode(source, node.Name);
            return node;
        }
        public override QueryNode Visit(SingleValuePropertyAccessNode node)
        {
            SingleValueNode? source = node.Source == null ? null : (SingleValueNode)Visit(node.Source);
            if (node.Source != source)
                node = new SingleValuePropertyAccessNode(source, node.Property);
            return node;
        }
        public override QueryNode Visit(UnaryOperatorNode node)
        {
            var operand = (SingleValueNode)Visit(node.Operand);
            return new UnaryOperatorNode(node.OperatorKind, operand);
        }
        public override QueryNode Visit(NamedFunctionParameterNode node)
        {
            QueryNode value = Visit(node.Value);
            if (node.Value != value)
                node = new NamedFunctionParameterNode(node.Name, value);
            return node;
        }
        public override QueryNode Visit(ParameterAliasNode node)
        {
            return node;
        }
        public override QueryNode Visit(SearchTermNode node)
        {
            return node;
        }
        public override QueryNode Visit(SingleComplexNode node)
        {
            SingleResourceNode? source = node.Source == null ? null : (SingleResourceNode)Visit(node.Source);
            return new SingleComplexNode(source, node.Property);
        }
        public override QueryNode Visit(CollectionComplexNode node)
        {
            SingleResourceNode? source = node.Source == null ? null : (SingleResourceNode)Visit(node.Source);
            if (node.Source != source)
                node = new CollectionComplexNode(source, node.Property);
            return node;
        }
        public override QueryNode Visit(SingleValueCastNode node)
        {
            SingleValueNode? source = node.Source == null ? null : (SingleValueNode)Visit(node.Source);
            if (node.Source != source)
                node = new SingleValueCastNode(source, (IEdmPrimitiveType)node.TypeReference.Definition);
            return node;
        }
        public override QueryNode Visit(AggregatedCollectionPropertyNode node)
        {
            CollectionNavigationNode? source = node.Source == null ? null : (CollectionNavigationNode)Visit(node.Source);
            if (node.Source != source)
                node = new AggregatedCollectionPropertyNode(source, node.Property);
            return node;
        }
        public override QueryNode Visit(InNode node)
        {
            var left = (SingleValueNode)Visit(node.Left);
            var right = (CollectionNode)Visit(node.Right);
            return new InNode(left, right);
        }
    }
}