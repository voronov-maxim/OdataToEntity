using Microsoft.OData.UriParser;
using OdataToEntity.Cache.UriCompare;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace OdataToEntity.Parsers
{
    public sealed class OeQueryNodeVisitor : QueryNodeVisitor<Expression>
    {
        private readonly Dictionary<ConstantExpression, ConstantNode> _constants;
        private ParameterExpression _parameter;
        private readonly OeQueryNodeVisitor? _parentVisitor;

        public OeQueryNodeVisitor(ParameterExpression parameter)
        {
            _parameter = parameter;

            _constants = new Dictionary<ConstantExpression, ConstantNode>();
        }
        public OeQueryNodeVisitor(ParameterExpression parameter, IReadOnlyDictionary<ConstantExpression, ConstantNode> constants)
                : this(parameter)
        {
            foreach (KeyValuePair<ConstantExpression, ConstantNode> pair in constants)
                AddConstant(pair.Key, pair.Value);
        }
        public OeQueryNodeVisitor(OeQueryNodeVisitor parentVisitor, ParameterExpression parameter)
                : this(parameter)
        {
            _parentVisitor = parentVisitor;
        }

        public void AddConstant(ConstantExpression constantExpression, ConstantNode constantNode)
        {
            if (_parentVisitor == null)
                _constants.Add(constantExpression, constantNode);
            else
                _parentVisitor.AddConstant(constantExpression, constantNode);
        }
        public ConstantExpression AddSkipConstant(int skip, ODataPath path)
        {
            ConstantExpression skipConstant = Expression.Constant(skip, typeof(int));
            ConstantNode skipNode = OeCacheComparerParameterValues.CreateSkipConstantNode(skip, path);
            AddConstant(skipConstant, skipNode);
            return skipConstant;
        }
        public ConstantExpression AddSkipTokenConstant(OeSkipTokenNameValue skipTokenNameValue, Type propertyType)
        {
            ConstantExpression skipTokenConstant = Expression.Constant(skipTokenNameValue.Value, propertyType);
            ConstantNode skipTokenNode = OeCacheComparerParameterValues.CreateSkipTokenConstantNode(skipTokenNameValue.Value, skipTokenNameValue.Name);
            AddConstant(skipTokenConstant, skipTokenNode);
            return skipTokenConstant;
        }
        public ConstantExpression AddTopConstant(int top, ODataPath path)
        {
            ConstantExpression topConstant = Expression.Constant(top, typeof(int));
            ConstantNode topNode = OeCacheComparerParameterValues.CreateTopConstantNode(top, path);
            AddConstant(topConstant, topNode);
            return topConstant;
        }
        public void ChangeParameterType(Expression source)
        {
            Type itemType = OeExpressionHelper.GetCollectionItemTypeOrNull(source.Type) ?? source.Type;
            if (Parameter.Type != itemType)
                _parameter = Expression.Parameter(itemType);
        }
        private Expression Lambda(CollectionNavigationNode sourceNode, SingleValueNode body, String methodName)
        {
            Expression source = TranslateNode(sourceNode);
            PropertyInfo sourceNavigationProperty = Parameter.Type.GetPropertyIgnoreCase(sourceNode.NavigationProperty);
            Type targetType = OeExpressionHelper.GetCollectionItemType(sourceNavigationProperty.PropertyType);

            ParameterExpression parameter = Expression.Parameter(targetType);
            Expression bodyExression = new OeQueryNodeVisitor(this, parameter).TranslateNode(body);
            LambdaExpression lambda = Expression.Lambda(bodyExression, parameter);
            return Expression.Call(typeof(Enumerable), methodName, new Type[] { targetType }, source, lambda);
        }
        private void ReplaceConstant(ConstantExpression oldExpression, ConstantExpression newExpression)
        {
            if (_parentVisitor == null)
            {
                ConstantNode constantNode = _constants[oldExpression];
                _constants.Remove(oldExpression);
                AddConstant(newExpression, constantNode);
            }
            else
                _parentVisitor.ReplaceConstant(oldExpression, newExpression);
        }
        public Expression TranslateNode(QueryNode node)
        {
            return node.Accept(this);
        }

        public override Expression Visit(AllNode nodeIn)
        {
            var sourceNode = (CollectionNavigationNode)nodeIn.Source;
            return Lambda(sourceNode, nodeIn.Body, nameof(Enumerable.All));
        }
        public override Expression Visit(AnyNode nodeIn)
        {
            var sourceNode = (CollectionNavigationNode)nodeIn.Source;
            return Lambda(sourceNode, nodeIn.Body, nameof(Enumerable.Any));
        }
        public override Expression Visit(BinaryOperatorNode nodeIn)
        {
            Expression left = TranslateNode(nodeIn.Left);
            Expression right = TranslateNode(nodeIn.Right);
            if (left.Type != right.Type)
            {
                Type leftType = left.Type;
                Type rightType = right.Type;

                Type? leftUnderlyingType = Nullable.GetUnderlyingType(left.Type);
                if (leftUnderlyingType != null && !OeExpressionHelper.IsNull(left))
                {
                    if (OeExpressionHelper.IsNull(right))
                        right = OeConstantToVariableVisitor.NullConstantExpression;
                    else
                        leftType = leftUnderlyingType;
                }
                else
                {
                    Type? rightUnderlyingType = Nullable.GetUnderlyingType(right.Type);
                    if (rightUnderlyingType != null && !OeExpressionHelper.IsNull(right))
                    {
                        if (OeExpressionHelper.IsNull(left))
                            left = OeConstantToVariableVisitor.NullConstantExpression;
                        else
                            rightType = rightUnderlyingType;
                    }
                }

                if (right.Type != left.Type)
                {
                    if (left is ConstantExpression leftOldConstant)
                    {
                        ConstantExpression newConstant = OeExpressionHelper.ConstantChangeType(leftOldConstant, rightType);
                        if (leftOldConstant != newConstant)
                            ReplaceConstant(leftOldConstant, newConstant);

                        if (newConstant.Value != null)
                            left = Expression.Convert(newConstant, right.Type);
                    }
                    else if (right is ConstantExpression rightOldConstant)
                    {
                        ConstantExpression newConstant = OeExpressionHelper.ConstantChangeType(rightOldConstant, leftType);
                        if (rightOldConstant != newConstant)
                            ReplaceConstant(rightOldConstant, newConstant);

                        if (newConstant.Value != null)
                            right = Expression.Convert(newConstant, left.Type);
                    }
                    else
                    {
                        Type precedenceType = OeExpressionHelper.GetTypeConversion(left.Type, right.Type);
                        if (left.Type != precedenceType)
                            left = Expression.Convert(left, precedenceType);
                        if (right.Type != precedenceType)
                            right = Expression.Convert(right, precedenceType);
                    }
                }
            }
            ExpressionType binaryType = OeExpressionHelper.ToExpressionType(nodeIn.OperatorKind);

            if (!(binaryType == ExpressionType.Equal || binaryType == ExpressionType.NotEqual))
            {
                if (left.Type == typeof(String))
                {
                    Func<String, String, int> compareToFunc = String.Compare;
                    MethodCallExpression compareToCall = Expression.Call(null, compareToFunc.GetMethodInfo(), left, right);
                    return Expression.MakeBinary(binaryType, compareToCall, OeConstantToVariableVisitor.ZeroStringCompareConstantExpression);
                }

                Type? underlyingType;
                if (left.Type.IsEnum)
                {
                    Type enumUnderlyingType = Enum.GetUnderlyingType(left.Type);
                    left = ConvertEnumExpression(left, enumUnderlyingType);
                    right = ConvertEnumExpression(right, enumUnderlyingType);
                }
                else if ((underlyingType = Nullable.GetUnderlyingType(left.Type)) != null && underlyingType.IsEnum)
                {
                    Type enumUnderlyingType = Enum.GetUnderlyingType(underlyingType);
                    Type nullableUnderlyingType = typeof(Nullable<>).MakeGenericType(enumUnderlyingType);
                    left = ConvertEnumExpression(left, nullableUnderlyingType);
                    right = ConvertEnumExpression(right, nullableUnderlyingType);
                }
            }

            return Expression.MakeBinary(binaryType, left, right);

            static UnaryExpression ConvertEnumExpression(Expression e, Type nullableType)
            {
                if (e is UnaryExpression unaryExpression)
                {
                    var value = (ConstantExpression)unaryExpression.Operand;
                    return Expression.Convert(value, nullableType);
                }

                return Expression.Convert(e, nullableType);
            }
        }
        public override Expression Visit(CollectionNavigationNode nodeIn)
        {
            Expression source = TranslateNode(nodeIn.Source);
            PropertyInfo propertyInfo = source.Type.GetPropertyIgnoreCase(nodeIn.NavigationProperty);
            return Expression.Property(source, propertyInfo);
        }
        public override Expression Visit(ConstantNode nodeIn)
        {
            if (nodeIn.Value == null)
                return OeConstantToVariableVisitor.NullConstantExpression;

            ConstantExpression e = Expression.Constant(nodeIn.Value);
            AddConstant(e, nodeIn);
            return e;
        }
        public override Expression Visit(ConvertNode nodeIn)
        {
            Expression e = TranslateNode(nodeIn.Source);
            if (e is ConstantExpression constantExpression && constantExpression.Value == null && constantExpression.Type == typeof(Object))
                return OeConstantToVariableVisitor.NullConstantExpression;
            return e;
        }
        public override Expression Visit(CountNode nodeIn)
        {
            var navigation = (CollectionNavigationNode)nodeIn.Source;
            PropertyInfo propertyInfo = Parameter.Type.GetPropertyIgnoreCase(navigation.NavigationProperty);
            MemberExpression property = Expression.Property(Parameter, propertyInfo);
            var typeArguments = new Type[] { OeExpressionHelper.GetCollectionItemType(property.Type) };
            return Expression.Call(typeof(Enumerable), nameof(Enumerable.Count), typeArguments, property);
        }
        public override Expression Visit(InNode nodeIn)
        {
            var propertyExpression = (MemberExpression)Visit((SingleValuePropertyAccessNode)nodeIn.Left);
            var constantNodes = (CollectionConstantNode)nodeIn.Right;

            BinaryExpression? inExpression = null;
            for (int i = 0; i < constantNodes.Collection.Count; i++)
            {
                var constantExpression = (ConstantExpression)Visit(constantNodes.Collection[i]);
                ConstantExpression coercedConstanExpression = OeExpressionHelper.ConstantChangeType(constantExpression, propertyExpression.Type);
                if (coercedConstanExpression != constantExpression)
                    ReplaceConstant(constantExpression, coercedConstanExpression);

                BinaryExpression equalExpression = Expression.Equal(propertyExpression, coercedConstanExpression);
                inExpression = inExpression == null ? equalExpression : Expression.OrElse(inExpression, equalExpression);
            }
            return inExpression!;
        }
        public override Expression Visit(ResourceRangeVariableReferenceNode nodeIn)
        {
            return Parameter;
        }
        public override Expression Visit(SingleNavigationNode nodeIn)
        {
            Expression source;
            if (nodeIn.Source is SingleNavigationNode sourceNode)
                source = Visit(sourceNode);
            else
                source = Parameter;

            PropertyInfo? propertyInfo = source.Type.GetPropertyIgnoreCaseOrNull(nodeIn.NavigationProperty);
            if (propertyInfo == null)
            {
                if (TuplePropertyByAliasName == null)
                    throw new InvalidOperationException("Cannot translate navigation property on Tuple type");

                return Parameter;
            }

            return Expression.Property(source, propertyInfo);
        }
        public override Expression Visit(SingleValuePropertyAccessNode nodeIn)
        {
            Expression e;
            if (TuplePropertyByAliasName == null)
            {
                e = TranslateNode(nodeIn.Source);
                PropertyInfo property = e.Type.GetPropertyIgnoreCase(nodeIn.Property);
                return Expression.Property(e, property);
            }

            Expression source = TranslateNode(nodeIn.Source);
            return TuplePropertyByAliasName(source, nodeIn);
        }
        public override Expression Visit(SingleValueFunctionCallNode nodeIn)
        {
            return OeFunctionBinder.Bind(this, nodeIn);
        }
        public override Expression Visit(SingleValueOpenPropertyAccessNode nodeIn)
        {
            Expression source = TranslateNode(nodeIn.Source);
            if (TuplePropertyByAliasName == null)
                throw new InvalidOperationException("Cannot transalte SingleValueOpenPropertyAccessNode " + nameof(TuplePropertyByAliasName) + " is null");

            return TuplePropertyByAliasName(source, nodeIn);
        }

        public IReadOnlyDictionary<ConstantExpression, ConstantNode> Constans => _parentVisitor == null ? _constants : _parentVisitor.Constans;
        public ParameterExpression Parameter => _parameter;
        public Func<Expression, SingleValueNode, Expression>? TuplePropertyByAliasName { get; set; }
    }
}