using Microsoft.OData.UriParser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OdataToEntity.Parsers.UriCompare
{
    public sealed class OeQueryNodeHashVisitor : QueryNodeVisitor<int>
    {
        public int TranslateNode(QueryNode node)
        {
            return node.Accept(this);
        }

        private static int CombineHashCodes(int h1, int h2)
        {
            return (h1 << 5) + h1 ^ h2;
        }
        private static int CombineHashCodes(int h1, int h2, int h3)
        {
            return CombineHashCodes(CombineHashCodes(h1, h2), h3);
        }

        public override int Visit(AllNode nodeIn)
        {
            var sourceNode = (CollectionNavigationNode)nodeIn.Source;
            int h1 = sourceNode.NavigationProperty.Name.GetHashCode();
            int h2 = nameof(Enumerable.All).GetHashCode();
            int h3 = TranslateNode(nodeIn.Body);
            return CombineHashCodes(h1, h2, h3);
        }
        public override int Visit(AnyNode nodeIn)
        {
            var sourceNode = (CollectionNavigationNode)nodeIn.Source;
            int h1 = sourceNode.NavigationProperty.Name.GetHashCode();
            int h2 = nameof(Enumerable.Any).GetHashCode();
            int h3 = TranslateNode(nodeIn.Body);
            return CombineHashCodes(h1, h2, h3);
        }
        public override int Visit(BinaryOperatorNode nodeIn)
        {
            int left = TranslateNode(nodeIn.Left);
            int right = TranslateNode(nodeIn.Right);
            return CombineHashCodes(left, right, (int)nodeIn.OperatorKind);
        }
        public override int Visit(CollectionNavigationNode nodeIn)
        {
            return nodeIn.NavigationProperty.Name.GetHashCode();
        }
        public override int Visit(ConstantNode nodeIn)
        {
            return 0;
        }
        public override int Visit(ConvertNode nodeIn)
        {
            return TranslateNode(nodeIn.Source);
        }
        public override int Visit(CountNode nodeIn)
        {
            var sourceNode = (CollectionNavigationNode)nodeIn.Source;
            return sourceNode.NavigationProperty.Name.GetHashCode();
        }
        public override int Visit(ResourceRangeVariableReferenceNode nodeIn)
        {
            return nodeIn.Name.GetHashCode();
        }
        public override int Visit(SingleNavigationNode nodeIn)
        {
            var sourceNode = nodeIn.Source as SingleNavigationNode;
            if (sourceNode != null)
                return sourceNode.NavigationProperty.Name.GetHashCode();

            return TranslateNode(nodeIn.Source);
        }
        public override int Visit(SingleValuePropertyAccessNode nodeIn)
        {
            return CombineHashCodes(TranslateNode(nodeIn.Source), nodeIn.Property.Name.GetHashCode());
        }
        public override int Visit(SingleValueFunctionCallNode nodeIn)
        {
            return nodeIn.Name.GetHashCode();
        }
        public override int Visit(SingleValueOpenPropertyAccessNode nodeIn)
        {
            return CombineHashCodes(TranslateNode(nodeIn.Source), nodeIn.Name.GetHashCode());
        }
    }
}
