using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace OdataToEntity.Parsers
{
    public sealed class OeQueryNodeVisitor : QueryNodeVisitor<Expression>
    {
        private sealed class TupleNavigationPropertyException : Exception
        {
            private readonly IEdmNavigationProperty _navigationProperty;

            public TupleNavigationPropertyException(IEdmNavigationProperty navigationProperty)
            {
                _navigationProperty = navigationProperty;
            }

            public IEdmNavigationProperty NavigationProperty => _navigationProperty;
        }

        private readonly IEdmModel _model;
        private readonly Stack<ParameterExpression> _parameters;

        public OeQueryNodeVisitor(IEdmModel model, ParameterExpression it)
        {
            _model = model;
            _parameters = new Stack<ParameterExpression>();
            _parameters.Push(it);
        }

        private Expression Lambda(CollectionNavigationNode sourceNode, SingleValueNode body, String methodName)
        {
            Expression source = TranslateNode(sourceNode);
            PropertyInfo sourceNavigationProperty = Parameter.Type.GetTypeInfo().GetProperty(sourceNode.NavigationProperty.Name);
            Type targetType = OeExpressionHelper.GetCollectionItemType(sourceNavigationProperty.PropertyType);

            ParameterExpression it = Expression.Parameter(targetType);
            _parameters.Push(it);
            var bodyExression = TranslateNode(body);
            _parameters.Pop();
            LambdaExpression lambda = Expression.Lambda(bodyExression, it);

            var typeArguments = new Type[] { it.Type };
            return Expression.Call(typeof(Enumerable), methodName, typeArguments, source, lambda);
        }
        public Expression TranslateNode(QueryNode node)
        {
            return node.Accept(this);
        }

        public override Expression Visit(BinaryOperatorNode nodeIn)
        {
            Expression left = TranslateNode(nodeIn.Left);
            Expression right = TranslateNode(nodeIn.Right);
            if (left.Type != right.Type && !(OeExpressionHelper.IsNull(left) || OeExpressionHelper.IsNull(right)))
            {
                Type leftType = left.Type;
                Type rightType = right.Type;
                if (OeExpressionHelper.IsNullable(left))
                {
                    leftType = Nullable.GetUnderlyingType(left.Type);
                    left = Expression.Convert(left, left.Type);
                }
                else if (OeExpressionHelper.IsNullable(right))
                {
                    rightType = Nullable.GetUnderlyingType(right.Type);
                    right = Expression.Convert(right, right.Type);
                }

                if (right.Type != left.Type)
                {
                    if (left is ConstantExpression)
                    {
                        left = OeExpressionHelper.ConstantChangeType(left as ConstantExpression, rightType);
                        left = Expression.Convert(left, right.Type);
                    }
                    else if (right is ConstantExpression)
                    {
                        right = OeExpressionHelper.ConstantChangeType(right as ConstantExpression, leftType);
                        right = Expression.Convert(right, left.Type);
                    }
                    else
                        right = Expression.Convert(right, left.Type);
                }
            }

            ExpressionType binaryType = OeExpressionHelper.ToExpressionType(nodeIn.OperatorKind);
            return Expression.MakeBinary(binaryType, left, right);
        }
        public override Expression Visit(ConstantNode nodeIn)
        {
            return Expression.Constant(nodeIn.Value);
        }
        public override Expression Visit(CountNode nodeIn)
        {
            var navigation = (CollectionNavigationNode)nodeIn.Source;
            MemberExpression property = Expression.Property(Parameter, navigation.NavigationProperty.Name);
            var typeArguments = new Type[] { OeExpressionHelper.GetCollectionItemType(property.Type) };
            return Expression.Call(typeof(Enumerable), nameof(Enumerable.Count), typeArguments, property);
        }
        public override Expression Visit(SingleValuePropertyAccessNode nodeIn)
        {
            if (TuplePropertyMapper == null)
                return Expression.Property(TranslateNode(nodeIn.Source), nodeIn.Property.Name);

            Expression source;
            String aliasName = nodeIn.Property.Name;
            try
            {
                source = TranslateNode(nodeIn.Source);
            }
            catch (TupleNavigationPropertyException e)
            {
                source = Parameter;
                aliasName = e.NavigationProperty.Name + "_" + aliasName;
            }
            return TuplePropertyMapper(source, aliasName);
        }
        public override Expression Visit(ResourceRangeVariableReferenceNode nodeIn)
        {
            return Parameter;
        }
        public override Expression Visit(ConvertNode nodeIn)
        {
            return TranslateNode(nodeIn.Source);
        }
        public override Expression Visit(SingleNavigationNode nodeIn)
        {
            Expression source;
            if (nodeIn.Source is SingleNavigationNode)
                source = TranslateNode(nodeIn.Source);
            else
                source = Parameter;

            PropertyInfo propertyInfo = source.Type.GetTypeInfo().GetProperty(nodeIn.NavigationProperty.Name);
            if (propertyInfo == null)
                if (TuplePropertyMapper == null)
                    throw new InvalidOperationException($"Navigation property {nodeIn.NavigationProperty.Name} not found");
                else
                    throw new TupleNavigationPropertyException(nodeIn.NavigationProperty);

            return Expression.Property(source, propertyInfo);
        }
        public override Expression Visit(CollectionNavigationNode nodeIn)
        {
            Expression source = TranslateNode(nodeIn.Source);
            PropertyInfo propertyInfo = source.Type.GetTypeInfo().GetProperty(nodeIn.NavigationProperty.Name);
            return Expression.Property(source, propertyInfo);
        }
        public override Expression Visit(AnyNode nodeIn)
        {
            var sourceNode = (CollectionNavigationNode)nodeIn.Source;
            return Lambda(sourceNode, nodeIn.Body, nameof(Enumerable.Any));
        }
        public override Expression Visit(AllNode nodeIn)
        {
            var sourceNode = (CollectionNavigationNode)nodeIn.Source;
            return Lambda(sourceNode, nodeIn.Body, nameof(Enumerable.All));
        }
        public override Expression Visit(SingleValueFunctionCallNode nodeIn)
        {
            return OeFunctionBinder.Bind(this, nodeIn);
        }
        public override Expression Visit(SingleValueOpenPropertyAccessNode nodeIn)
        {
            Expression source = TranslateNode(nodeIn.Source);
            if (TuplePropertyMapper == null)
                return Expression.Property(source, nodeIn.Name);
            else
                return TuplePropertyMapper(source, nodeIn.Name);
        }

        public ParameterExpression Parameter => _parameters.Peek();
        public Func<Expression, String, Expression> TuplePropertyMapper { get; set; }
    }
}
