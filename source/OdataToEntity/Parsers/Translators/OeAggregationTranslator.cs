using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using Microsoft.OData.UriParser.Aggregation;
using OdataToEntity.ModelBuilder;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace OdataToEntity.Parsers.Translators
{
    public readonly struct OeAggregationTranslator
    {
        private readonly struct ComputeAliasNameResolver
        {
            private readonly List<AggProperty> _aggProperties;
            private readonly List<Expression> _aggExpressions;

            public ComputeAliasNameResolver(List<AggProperty> aggProperties, List<Expression> aggExpressions)
            {
                _aggProperties = aggProperties;
                _aggExpressions = aggExpressions;
            }

            public Expression GetTuplePropertyByAliasName(Expression source, SingleValueNode singleValueNode)
            {
                String aliasName = GetAliasName(singleValueNode);
                int i = _aggProperties.FindIndex(p => p.Name == aliasName);
                if (_aggProperties[i].IsGroup)
                    return OeExpressionHelper.GetPropertyExpressions(_aggExpressions[0])[i];

                int groupCount;
                for (groupCount = 0; groupCount < _aggProperties.Count && _aggProperties[groupCount].IsGroup; groupCount++)
                {
                }
                return _aggExpressions[i - groupCount];
            }
        }

        internal sealed class AggProperty : EdmStructuralProperty
        {
            public AggProperty(String name, IEdmTypeReference type, bool isGroup) : base(PrimitiveTypeHelper.TupleEdmType, name, type)
            {
                IsGroup = isGroup;
            }

            public bool IsGroup { get; }
        }

        private readonly List<AggProperty> _aggProperties;
        private readonly OeQueryNodeVisitor _visitor;

        public OeAggregationTranslator(OeQueryNodeVisitor visitor)
        {
            _visitor = visitor;
            _aggProperties = new List<AggProperty>();
        }

        private static MethodCallExpression AggCallExpression(AggregationMethod aggMethod, ParameterExpression sourceParameter, LambdaExpression lambda)
        {
            String methodName;
            switch (aggMethod)
            {
                case AggregationMethod.Average:
                    methodName = nameof(Enumerable.Average);
                    break;
                case AggregationMethod.CountDistinct:
                    return CountDistinctExpression(sourceParameter, lambda);
                case AggregationMethod.Max:
                    methodName = nameof(Enumerable.Max);
                    break;
                case AggregationMethod.Min:
                    methodName = nameof(Enumerable.Min);
                    break;
                case AggregationMethod.Sum:
                    methodName = nameof(Enumerable.Sum);
                    break;
                case AggregationMethod.VirtualPropertyCount:
                    return CountExpression(sourceParameter);
                default:
                    throw new NotSupportedException();
            }

            MethodInfo closeMethod;
            MethodInfo openMethod = OeMethodInfoHelper.GetAggMethodInfo(methodName, lambda.ReturnType);
            if (openMethod.GetGenericArguments().Length == 1)
                closeMethod = openMethod.MakeGenericMethod(lambda.Parameters[0].Type);
            else
                closeMethod = openMethod.MakeGenericMethod(lambda.Parameters[0].Type, lambda.ReturnType);

            return Expression.Call(closeMethod, sourceParameter, lambda);
        }
        private MethodCallExpression ApplyAggregate(Expression source, AggregateTransformationNode transformation)
        {
            Type sourceType = OeExpressionHelper.GetCollectionItemType(source.Type);
            ParameterExpression sourceParameter = Expression.Parameter(sourceType);
            ParameterExpression lambdaParameter = sourceParameter;

            var expressions = new List<Expression>();
            bool isGroupBy = sourceType.GetGenericTypeDefinition() == typeof(IGrouping<,>);
            if (isGroupBy)
            {
                PropertyInfo keyProperty = sourceType.GetProperty(nameof(IGrouping<Object, Object>.Key))!;
                MemberExpression key = Expression.Property(sourceParameter, keyProperty);
                expressions.Add(key);

                lambdaParameter = Expression.Parameter(sourceType.GetGenericArguments()[1]);
            }

            var visitor = CreateVisitor(lambdaParameter);
            foreach (AggregateExpressionBase aggExpressionBase in transformation.AggregateExpressions)
            {
                if (aggExpressionBase is AggregateExpression aggExpression)
                {
                    MethodCallExpression aggCallExpression;
                    if (aggExpression.Method == AggregationMethod.VirtualPropertyCount)
                        aggCallExpression = CountExpression(sourceParameter);
                    else
                    {
                        Expression expression = visitor.TranslateNode(aggExpression.Expression);
                        if (isGroupBy && expression is MemberExpression propertyExpression)
                        {
                            MemberExpression? keyPropertyExpression = FindInGroupByKey(source, expressions[0], propertyExpression);
                            if (keyPropertyExpression != null)
                                expression = keyPropertyExpression;
                        }
                        LambdaExpression aggLambda = Expression.Lambda(expression, lambdaParameter);
                        aggCallExpression = AggCallExpression(aggExpression.Method, sourceParameter, aggLambda);
                    }
                    expressions.Add(aggCallExpression);
                    _aggProperties.Add(CreateEdmProperty(aggCallExpression.Type, aggExpression.Alias, false));
                }
                else
                    throw new NotSupportedException("Unknown aggregate expression type " + aggExpressionBase.GetType().Name);
            }

            NewExpression newExpression = OeExpressionHelper.CreateTupleExpression(expressions);
            MethodInfo selectMethodInfo = OeMethodInfoHelper.GetSelectMethodInfo(sourceType, newExpression.Type);
            LambdaExpression lambda = Expression.Lambda(newExpression, sourceParameter);
            return Expression.Call(selectMethodInfo, source, lambda);
        }
        private MethodCallExpression ApplyCompute(Expression source, ComputeTransformationNode transformation)
        {
            var expressions = new List<Expression>();

            Type sourceType = OeExpressionHelper.GetCollectionItemType(source.Type);
            ParameterExpression sourceParameter = Expression.Parameter(sourceType);

            if (_aggProperties.Count > 0)
            {
                var callExpression = (MethodCallExpression)source;
                source = callExpression.Arguments[0];
                var aggLambda = (LambdaExpression)callExpression.Arguments[1];
                expressions.AddRange(((NewExpression)aggLambda.Body).Arguments);
                sourceParameter = aggLambda.Parameters[0];
            }

            OeQueryNodeVisitor visitor = CreateVisitor(sourceParameter);
            if (_aggProperties.Count > 0)
                visitor.TuplePropertyByAliasName = new ComputeAliasNameResolver(_aggProperties, expressions).GetTuplePropertyByAliasName;

            foreach (ComputeExpression computeExpression in transformation.Expressions)
            {
                Expression expression = visitor.TranslateNode(computeExpression.Expression);
                expressions.Add(expression);

                _aggProperties.Add(CreateEdmProperty(expression.Type, computeExpression.Alias, false));
            }

            NewExpression newExpression = OeExpressionHelper.CreateTupleExpression(expressions);
            MethodInfo selectMethodInfo = OeMethodInfoHelper.GetSelectMethodInfo(sourceParameter.Type, newExpression.Type);
            LambdaExpression lambda = Expression.Lambda(newExpression, sourceParameter);
            return Expression.Call(selectMethodInfo, source, lambda);
        }
        private MethodCallExpression ApplyFilter(Expression source, FilterTransformationNode transformation)
        {
            Type sourceType = OeExpressionHelper.GetCollectionItemType(source.Type);
            ParameterExpression sourceParameter = Expression.Parameter(sourceType);

            var visitor = CreateVisitor(sourceParameter);
            if (_aggProperties.Count > 0)
                visitor.TuplePropertyByAliasName = GetTuplePropertyByAliasName;
            Expression e = visitor.TranslateNode(transformation.FilterClause.Expression);

            MethodInfo whereMethodInfo = OeMethodInfoHelper.GetWhereMethodInfo(sourceParameter.Type);
            LambdaExpression lambda = Expression.Lambda(e, sourceParameter);
            return Expression.Call(whereMethodInfo, source, lambda);
        }
        private MethodCallExpression ApplyGroupBy(Expression source, GroupByTransformationNode transformation)
        {
            Type sourceType = OeExpressionHelper.GetCollectionItemType(source.Type);
            ParameterExpression sourceParameter = Expression.Parameter(sourceType);
            var visitor = CreateVisitor(sourceParameter);

            var expressions = new List<Expression>();
            foreach (GroupByPropertyNode node in transformation.GroupingProperties)
                if (node.ChildTransformations != null && node.ChildTransformations.Count > 0)
                {
                    for (int i = 0; i < node.ChildTransformations.Count; i++)
                    {
                        Expression e = visitor.TranslateNode(node.ChildTransformations[i].Expression);
                        expressions.Add(e);

                        String aliasName = node.Name + "_" + node.ChildTransformations[i].Name;
                        _aggProperties.Add(CreateEdmProperty(e.Type, aliasName, true));
                    }
                }
                else
                {
                    Expression e = visitor.TranslateNode(node.Expression);
                    expressions.Add(e);

                    _aggProperties.Add(CreateEdmProperty(e.Type, node.Name, true));
                }

            NewExpression newExpression = OeExpressionHelper.CreateTupleExpression(expressions);
            LambdaExpression lambda = Expression.Lambda(newExpression, sourceParameter);

            MethodInfo groupByMethodInfo = OeMethodInfoHelper.GetGroupByMethodInfo(sourceType, newExpression.Type);
            MethodCallExpression groupByCall = Expression.Call(groupByMethodInfo, source, lambda);

            var aggTransformation = (AggregateTransformationNode)transformation.ChildTransformations;
            if (aggTransformation == null)
            {
                expressions.Clear();
                sourceType = OeExpressionHelper.GetCollectionItemType(groupByCall.Type);
                sourceParameter = Expression.Parameter(sourceType);
                expressions.Add(Expression.Property(sourceParameter, nameof(IGrouping<Object, Object>.Key)));
                newExpression = OeExpressionHelper.CreateTupleExpression(expressions);

                MethodInfo selectMethodInfo = OeMethodInfoHelper.GetSelectMethodInfo(sourceType, newExpression.Type);
                lambda = Expression.Lambda(newExpression, sourceParameter);
                return Expression.Call(selectMethodInfo, groupByCall, lambda);
            }

            return ApplyAggregate(groupByCall, aggTransformation);
        }
        public Expression Build(Expression source, ApplyClause applyClause, out OeEntryFactoryFactory entryFactoryFactory)
        {
            entryFactoryFactory = new OeAggregationEntryFactoryFactory(_aggProperties);

            _aggProperties.Clear();
            if (applyClause == null)
                return source;

            foreach (TransformationNode transformation in applyClause.Transformations)
            {
                if (transformation is GroupByTransformationNode groupTransformation)
                    source = ApplyGroupBy(source, groupTransformation);
                else if (transformation is FilterTransformationNode filterTransformation)
                    source = ApplyFilter(source, filterTransformation);
                else if (transformation is ComputeTransformationNode computeTransformation)
                    source = ApplyCompute(source, computeTransformation);
                else if (transformation is AggregateTransformationNode)
                    throw new NotSupportedException();
                else
                    throw new NotSupportedException();
            }

            return source;
        }
        private static MethodCallExpression CountDistinctExpression(ParameterExpression sourceParameter, LambdaExpression lambda)
        {
            MethodInfo selectMetodInfo = OeMethodInfoHelper.GetSelectMethodInfo(lambda.Parameters[0].Type, lambda.ReturnType);
            MethodCallExpression selectCall = Expression.Call(selectMetodInfo, sourceParameter, lambda);

            MethodInfo distinctMethodInfo = OeMethodInfoHelper.GetDistinctMethodInfo(lambda.ReturnType);
            MethodCallExpression distinctCall = Expression.Call(distinctMethodInfo, selectCall);

            MethodInfo countMethodInfo = OeMethodInfoHelper.GetCountMethodInfo(lambda.ReturnType);
            return Expression.Call(countMethodInfo, distinctCall);
        }
        private static MethodCallExpression CountExpression(ParameterExpression sourceParameter)
        {
            Type itemType = OeExpressionHelper.GetCollectionItemType(sourceParameter.Type);
            MethodInfo countMethodInfo = OeMethodInfoHelper.GetCountMethodInfo(itemType);
            return Expression.Call(countMethodInfo, sourceParameter);
        }
        private static AggProperty CreateEdmProperty(Type clrType, String name, bool isGroup)
        {
            return new AggProperty(name, OeEdmClrHelper.GetEdmTypeReference(clrType), isGroup);
        }
        private OeQueryNodeVisitor CreateVisitor(ParameterExpression parameter)
        {
            return new OeQueryNodeVisitor(_visitor, parameter);
        }
        private static MemberExpression? FindInGroupByKey(Expression source, Expression key, MemberExpression propertyExpression)
        {
            if (propertyExpression.Expression is MemberExpression)
            {
                var propertyTranslator = new OePropertyTranslator(source);
                return propertyTranslator.Build(key, (PropertyInfo)propertyExpression.Member);
            }

            return null;
        }
        private static String GetAliasName(SingleValueNode singleValueNode)
        {
            if (singleValueNode is SingleValuePropertyAccessNode propertyNode)
            {
                if (propertyNode.Source is ResourceRangeVariableReferenceNode)
                    return propertyNode.Property.Name;

                if (propertyNode.Source is SingleNavigationNode navigationNode)
                    return navigationNode.NavigationProperty.Name + "_" + propertyNode.Property.Name;

                throw new NotSupportedException("SingleValuePropertyAccessNode.Source type " + propertyNode.Source.GetType().FullName);
            }

            if (singleValueNode is SingleValueOpenPropertyAccessNode openPropertyNode)
                return openPropertyNode.Name;

            throw new ArgumentException("invalid type", nameof(singleValueNode));
        }
        internal Expression GetTuplePropertyByAliasName(Expression source, SingleValueNode singleValueNode)
        {
            String aliasName = GetAliasName(singleValueNode);
            int groupCount = 0;
            for (int i = 0; i < _aggProperties.Count; i++)
            {
                if (_aggProperties[i].IsGroup)
                    groupCount++;
                if (String.CompareOrdinal(_aggProperties[i].Name, aliasName) == 0)
                {
                    PropertyInfo propertyInfo;
                    int itemIndex;
                    if (_aggProperties[i].IsGroup)
                    {
                        propertyInfo = source.Type.GetProperty("Item1")!;
                        source = Expression.Property(source, propertyInfo);
                        itemIndex = groupCount;

                        for (; itemIndex > 7; itemIndex -= 7)
                        {
                            propertyInfo = source.Type.GetProperty("Rest")!;
                            source = Expression.Property(source, propertyInfo);
                        }
                    }
                    else
                        itemIndex = i - groupCount + 2;

                    String propertyName = "Item" + itemIndex.ToString(CultureInfo.InvariantCulture);
                    propertyInfo = source.Type.GetProperty(propertyName)!;
                    return Expression.Property(source, propertyInfo);
                }
            }

            throw new InvalidOperationException("Property " + aliasName + " not found in " + source.Type.FullName);
        }
    }
}
