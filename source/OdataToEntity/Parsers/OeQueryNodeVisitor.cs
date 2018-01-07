using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using OdataToEntity.ModelBuilder;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace OdataToEntity.Parsers
{
    public sealed class OeQueryNodeVisitor : QueryNodeVisitor<Expression>
    {
        public sealed class NavigationPropertyNotFoundException : Exception
        {
            private readonly IEdmNavigationProperty _navigationProperty;

            public NavigationPropertyNotFoundException(IEdmNavigationProperty navigationProperty) :
                base("Navigation property " + navigationProperty.Name + " not found")
            {
                _navigationProperty = navigationProperty;
            }

            public IEdmNavigationProperty NavigationProperty => _navigationProperty;
        }

        private readonly Dictionary<ConstantExpression, ConstantNode> _constants;
        private readonly IEdmModel _edmModel;
        private readonly Stack<ParameterExpression> _parameters;
        private readonly OeQueryNodeVisitor _parentVisitor;

        public OeQueryNodeVisitor(IEdmModel edmModel, ParameterExpression it)
        {
            _edmModel = edmModel;
            _parameters = new Stack<ParameterExpression>();
            _parameters.Push(it);

            _constants = new Dictionary<ConstantExpression, ConstantNode>();
        }
        public OeQueryNodeVisitor(IEdmModel edmModel, ParameterExpression it, IReadOnlyDictionary<ConstantExpression, ConstantNode> constants)
                : this(edmModel, it)
        {
            foreach (KeyValuePair<ConstantExpression, ConstantNode> pair in constants)
                AddConstant(pair.Key, pair.Value);
        }
        public OeQueryNodeVisitor(OeQueryNodeVisitor parentVisitor, ParameterExpression it)
                : this(parentVisitor._edmModel, it)
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
        public void AddSkipConstant(ConstantExpression skipConstant, ODataPath path)
        {
            ConstantNode skipNode = UriCompare.OeCacheComparerParameterValues.CreateSkipConstantNode((int)skipConstant.Value, path);
            AddConstant(skipConstant, skipNode);
        }
        public void AddSkipTokenConstant(ConstantExpression skipTokenConstant, String propertyName)
        {
            ConstantNode skipTokenNode = UriCompare.OeCacheComparerParameterValues.CreateSkipTokenConstantNode(skipTokenConstant.Value, propertyName);
            AddConstant(skipTokenConstant, skipTokenNode);
        }
        public void AddTopConstant(ConstantExpression topConstant, ODataPath path)
        {
            ConstantNode topNode = UriCompare.OeCacheComparerParameterValues.CreateTopConstantNode((int)topConstant.Value, path);
            AddConstant(topConstant, topNode);
        }
        private Expression GetPropertyExpression(SingleValuePropertyAccessNode nodeIn)
        {
            Expression e = TranslateNode(nodeIn.Source);
            PropertyInfo property = e.Type.GetProperty(nodeIn.Property.Name);
            if (property == null)
            {
                if (!OeExpressionHelper.IsTupleType(e.Type))
                    throw new InvalidOperationException("must by Tuple " + e.Type.ToString());

                IEdmNavigationSource navigationSource = ((ResourceRangeVariableReferenceNode)nodeIn.Source).NavigationSource;
                property = GetTuplePropertyByEntityType(e.Type, navigationSource.EntityType());
                if (property == null)
                {
                    if (TuplePropertyByEdmProperty == null)
                        throw new InvalidOperationException("entity type " + navigationSource.EntityType().FullName() + " not found in tuple properties");

                    return TuplePropertyByEdmProperty(Parameter, nodeIn.Property);
                }
                else
                {
                    e = Expression.Property(e, property);
                    property = e.Type.GetProperty(nodeIn.Property.Name);
                }
            }
            return Expression.Property(e, property);
        }
        private static PropertyInfo GetTuplePropertyByEntityType(Type tupleType, IEdmEntityType edmEntityType)
        {
            String fullName = edmEntityType.FullName();
            foreach (PropertyInfo propertyInfo in tupleType.GetProperties())
                if (propertyInfo.PropertyType.FullName == fullName)
                    return propertyInfo;

            return null;
        }
        private Expression Lambda(CollectionNavigationNode sourceNode, SingleValueNode body, String methodName)
        {
            Expression source = TranslateNode(sourceNode);
            PropertyInfo sourceNavigationProperty = Parameter.Type.GetProperty(sourceNode.NavigationProperty.Name);
            Type targetType = OeExpressionHelper.GetCollectionItemType(sourceNavigationProperty.PropertyType);

            ParameterExpression it = Expression.Parameter(targetType);
            _parameters.Push(it);
            var bodyExression = TranslateNode(body);
            _parameters.Pop();
            LambdaExpression lambda = Expression.Lambda(bodyExression, it);

            var typeArguments = new Type[] { it.Type };
            return Expression.Call(typeof(Enumerable), methodName, typeArguments, source, lambda);
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

                if (OeExpressionHelper.IsNullable(left) && !OeExpressionHelper.IsNull(left))
                {
                    if (OeExpressionHelper.IsNull(right))
                    {
                        ConstantExpression newConstant = Expression.Constant(null, leftType);
                        ReplaceConstant((ConstantExpression)right, newConstant);
                        right = newConstant;
                    }
                    else
                        leftType = Nullable.GetUnderlyingType(left.Type);
                }
                else if (OeExpressionHelper.IsNullable(right) && !OeExpressionHelper.IsNull(right))
                {
                    if (OeExpressionHelper.IsNull(left))
                    {
                        ConstantExpression newConstant = Expression.Constant(null, rightType);
                        ReplaceConstant((ConstantExpression)left, newConstant);
                        left = newConstant;
                    }
                    else
                        rightType = Nullable.GetUnderlyingType(right.Type);
                }

                if (right.Type != left.Type)
                {
                    if (left is ConstantExpression)
                    {
                        ConstantExpression oldConstant = left as ConstantExpression;
                        ConstantExpression newConstant = OeExpressionHelper.ConstantChangeType(oldConstant, rightType);
                        if (oldConstant != newConstant)
                            ReplaceConstant(oldConstant, newConstant);
                        left = Expression.Convert(newConstant, right.Type);
                    }
                    else if (right is ConstantExpression)
                    {
                        ConstantExpression oldConstant = right as ConstantExpression;
                        ConstantExpression newConstant = OeExpressionHelper.ConstantChangeType(oldConstant, leftType);
                        if (oldConstant != newConstant)
                            ReplaceConstant(oldConstant, newConstant);
                        right = Expression.Convert(newConstant, left.Type);
                    }
                    else
                        right = Expression.Convert(right, left.Type);
                }
            }
            ExpressionType binaryType = OeExpressionHelper.ToExpressionType(nodeIn.OperatorKind);

            if (left.Type == typeof(String) && !(binaryType == ExpressionType.Equal || binaryType == ExpressionType.NotEqual))
            {
                Func<String, String, int> compareToFunc = String.Compare;
                MethodCallExpression compareToCall = Expression.Call(null, compareToFunc.GetMethodInfo(), left, right);
                return Expression.MakeBinary(binaryType, compareToCall, OeConstantToVariableVisitor.ZeroStringCompareConstantExpression);
            }

            return Expression.MakeBinary(binaryType, left, right);
        }
        public override Expression Visit(CollectionNavigationNode nodeIn)
        {
            Expression source = TranslateNode(nodeIn.Source);
            PropertyInfo propertyInfo = source.Type.GetProperty(nodeIn.NavigationProperty.Name);
            return Expression.Property(source, propertyInfo);
        }
        public override Expression Visit(ConstantNode nodeIn)
        {
            ConstantExpression e = Expression.Constant(nodeIn.Value);
            AddConstant(e, nodeIn);
            return e;
        }
        public override Expression Visit(ConvertNode nodeIn)
        {
            Expression e = TranslateNode(nodeIn.Source);
            if (e.NodeType == ExpressionType.Constant)
            {
                var constantExpression = e as ConstantExpression;
                if (constantExpression.Value == null && constantExpression.Type == typeof(Object))
                {
                    Type clrType;
                    EdmPrimitiveTypeKind primitiveTypeKind = nodeIn.TypeReference.PrimitiveKind();
                    if (primitiveTypeKind == EdmPrimitiveTypeKind.None)
                    {
                        if (nodeIn.TypeReference.IsEnum())
                            clrType = _edmModel.GetClrType(nodeIn.TypeReference.Definition);
                        else
                            throw new NotSupportedException(nodeIn.TypeReference.FullName());
                    }
                    else
                        clrType = PrimitiveTypeHelper.GetClrType(primitiveTypeKind);
                    if (nodeIn.TypeReference.IsNullable && clrType.IsValueType)
                        clrType = typeof(Nullable<>).MakeGenericType(clrType);

                    ConstantExpression newConstantExpression = Expression.Constant(null, clrType);
                    ReplaceConstant(constantExpression, newConstantExpression);
                    e = newConstantExpression;
                }
            }
            return e;
        }
        public override Expression Visit(CountNode nodeIn)
        {
            var navigation = (CollectionNavigationNode)nodeIn.Source;
            MemberExpression property = Expression.Property(Parameter, navigation.NavigationProperty.Name);
            var typeArguments = new Type[] { OeExpressionHelper.GetCollectionItemType(property.Type) };
            return Expression.Call(typeof(Enumerable), nameof(Enumerable.Count), typeArguments, property);
        }
        public override Expression Visit(ResourceRangeVariableReferenceNode nodeIn)
        {
            return Parameter;
        }
        public override Expression Visit(SingleNavigationNode nodeIn)
        {
            Expression source;
            if (nodeIn.Source is SingleNavigationNode)
                source = TranslateNode(nodeIn.Source);
            else
                source = Parameter;

            PropertyInfo propertyInfo = source.Type.GetProperty(nodeIn.NavigationProperty.Name);
            if (propertyInfo == null)
            {
                if (TuplePropertyByAliasName == null)
                    throw new NavigationPropertyNotFoundException(nodeIn.NavigationProperty);

                return null;
            }

            return Expression.Property(source, propertyInfo);
        }
        public override Expression Visit(SingleValuePropertyAccessNode nodeIn)
        {
            Expression e;
            if (TuplePropertyByAliasName == null)
            {
                e = TranslateNode(nodeIn.Source);
                PropertyInfo property = e.Type.GetProperty(nodeIn.Property.Name);
                if (property == null)
                {
                    if (!OeExpressionHelper.IsTupleType(e.Type))
                        throw new InvalidOperationException("must by Tuple " + e.Type.ToString());

                    IEdmNavigationSource navigationSource = ((ResourceRangeVariableReferenceNode)nodeIn.Source).NavigationSource;
                    property = GetTuplePropertyByEntityType(e.Type, navigationSource.EntityType());
                    if (property == null)
                    {
                        if (TuplePropertyByEdmProperty == null)
                            throw new InvalidOperationException("entity type " + navigationSource.EntityType().FullName() + " not found in tuple properties");

                        return TuplePropertyByEdmProperty(Parameter, nodeIn.Property);
                    }
                    else
                    {
                        e = Expression.Property(e, property);
                        property = e.Type.GetProperty(nodeIn.Property.Name);
                    }
                }
                return Expression.Property(e, property);
            }

            Expression source = TranslateNode(nodeIn.Source);
            if (source == null)
            {
                var navigationNode = (SingleNavigationNode)nodeIn.Source;
                String aliasName = navigationNode.NavigationProperty.Name + "_" + nodeIn.Property.Name;
                return TuplePropertyByAliasName(Parameter, aliasName);
            }

            e = TuplePropertyByAliasName(source, nodeIn.Property.Name);
            if (e == null)
            {
                e = GetPropertyExpression(nodeIn);
                if (e == null)
                    throw new InvalidOperationException("property name " + nodeIn.Property.Name + " not found");
            }
            return e;
        }
        public override Expression Visit(SingleValueFunctionCallNode nodeIn)
        {
            return OeFunctionBinder.Bind(this, nodeIn);
        }
        public override Expression Visit(SingleValueOpenPropertyAccessNode nodeIn)
        {
            Expression source = TranslateNode(nodeIn.Source);
            if (TuplePropertyByAliasName == null)
                return Expression.Property(source, nodeIn.Name);
            else
                return TuplePropertyByAliasName(source, nodeIn.Name);
        }

        public IReadOnlyDictionary<ConstantExpression, ConstantNode> Constans => _parentVisitor == null ? _constants : _parentVisitor.Constans;
        public IEdmModel EdmModel => _edmModel;
        public ParameterExpression Parameter => _parameters.Peek();
        public Func<Expression, String, Expression> TuplePropertyByAliasName { get; set; }
        public Func<Expression, IEdmProperty, Expression> TuplePropertyByEdmProperty { get; set; }
    }
}
