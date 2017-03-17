using Microsoft.OData.Edm;
using Microsoft.OData.UriParser.Aggregation;
using OdataToEntity.ModelBuilder;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace OdataToEntity.Parsers
{
    public sealed class OeAggregationTranslator
    {
        private sealed class AggProperty : EdmStructuralProperty
        {
            private readonly bool _isGroup;

            public AggProperty(String name, IEdmTypeReference type, bool isGroup) : base(PrimitiveTypeHelper.TupleEdmType, name, type)
            {
                _isGroup = isGroup;
            }

            public bool IsGroup => _isGroup;
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
                default:
                    throw new NotSupportedException();
            }

            MethodInfo closeMethod;
            MethodInfo openMethod = OeMethodInfoHelper.GetAggMethodInfo(methodName, lambda.ReturnType);
            if (openMethod == null)
            {
                Func<IEnumerable<Object>, Func<Object, Object>, Object> aggFunc;
                switch (aggMethod)
                {
                    case AggregationMethod.Max:
                        aggFunc = Enumerable.Max<Object, Object>;
                        break;
                    case AggregationMethod.Min:
                        aggFunc = Enumerable.Min<Object, Object>;
                        break;
                    default:
                        throw new InvalidOperationException($"Enumerable.{methodName} not found"); ;
                }
                openMethod = aggFunc.GetMethodInfo().GetGenericMethodDefinition();
                closeMethod = openMethod.MakeGenericMethod(lambda.Parameters[0].Type, lambda.ReturnType);
            }
            else
                closeMethod = openMethod.MakeGenericMethod(lambda.Parameters[0].Type);

            return Expression.Call(closeMethod, sourceParameter, lambda);
        }
        private MethodCallExpression ApplyAggregate(Expression source, AggregateTransformationNode transformation)
        {
            Type sourceType = OeExpressionHelper.GetCollectionItemType(source.Type);
            ParameterExpression sourceParameter = Expression.Parameter(sourceType);
            ParameterExpression lambdaParameter = sourceParameter;

            var expressions = new List<Expression>();
            if (sourceType.GetGenericTypeDefinition() == typeof(IGrouping<,>))
            {
                PropertyInfo keyProperty = sourceType.GetTypeInfo().GetProperty(nameof(IGrouping<Object, Object>.Key));
                MemberExpression key = Expression.Property(sourceParameter, keyProperty);
                expressions.Add(key);

                lambdaParameter = Expression.Parameter(sourceType.GetTypeInfo().GetGenericArguments()[1]);
            }

            var visitor = CreateVisitor(lambdaParameter);
            foreach (AggregateExpression aggExpression in transformation.Expressions)
            {
                Expression e = visitor.TranslateNode(aggExpression.Expression);
                LambdaExpression aggLambda = Expression.Lambda(e, lambdaParameter);
                MethodCallExpression aggCallExpression = AggCallExpression(aggExpression.Method, sourceParameter, aggLambda);
                expressions.Add(aggCallExpression);

                _aggProperties.Add(CreateEdmProperty(_visitor.EdmModel, aggCallExpression.Type, aggExpression.Alias, false));
            }

            NewExpression newExpression = OeExpressionHelper.CreateTupleExpression(expressions);
            MethodInfo selectMethodInfo = OeMethodInfoHelper.GetSelectMethodInfo(sourceType, newExpression.Type);
            LambdaExpression lambda = Expression.Lambda(newExpression, sourceParameter);
            return Expression.Call(selectMethodInfo, source, lambda);
        }
        private MethodCallExpression ApplyFilter(Expression source, FilterTransformationNode transformation)
        {
            Type sourceType = OeExpressionHelper.GetCollectionItemType(source.Type);
            ParameterExpression sourceParameter = Expression.Parameter(sourceType);

            var visitor = CreateVisitor(sourceParameter);
            if (_aggProperties.Count > 0)
                visitor.TuplePropertyMapper = TuplePropertyMapper;
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
                    if (node.ChildTransformations.Count > 1)
                        throw new NotSupportedException();

                    GroupByPropertyNode childNode = node.ChildTransformations[0];
                    String propertyName = node.Name + "_" + childNode.Name;

                    Expression e = visitor.TranslateNode(childNode.Expression);
                    expressions.Add(e);

                    _aggProperties.Add(CreateEdmProperty(_visitor.EdmModel, e.Type, propertyName, true));
                }
                else
                {
                    Expression e = visitor.TranslateNode(node.Expression);
                    expressions.Add(e);

                    _aggProperties.Add(CreateEdmProperty(_visitor.EdmModel, e.Type, node.Name, true));
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
        public Expression Build(Expression source, ApplyClause applyClause)
        {
            _aggProperties.Clear();
            if (applyClause == null)
                return source;

            foreach (TransformationNode transformation in applyClause.Transformations)
            {
                if (transformation is GroupByTransformationNode)
                {
                    var groupTransformation = transformation as GroupByTransformationNode;
                    source = ApplyGroupBy(source, groupTransformation);
                }
                else if (transformation is AggregateTransformationNode)
                {
                    throw new NotSupportedException();
                }
                else if (transformation is FilterTransformationNode)
                {
                    var filterTransformation = transformation as FilterTransformationNode;
                    source = ApplyFilter(source, filterTransformation);
                }
                else
                {
                    throw new NotSupportedException();
                }
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
        private static AggProperty CreateEdmProperty(IEdmModel model, Type clrType, String name, bool isGroup)
        {
            Type underlyingType = Nullable.GetUnderlyingType(clrType);
            if (underlyingType != null)
                clrType = underlyingType;

            bool nullable = PrimitiveTypeHelper.IsNullable(clrType);
            IEdmTypeReference edmTypeRef;
            if (clrType.GetTypeInfo().IsEnum)
            {
                var edmEnumType = (IEdmEnumType)model.FindType(clrType.FullName);
                edmTypeRef = new EdmEnumTypeReference(edmEnumType, nullable);
            }
            else
                edmTypeRef = PrimitiveTypeHelper.GetPrimitiveTypeRef(clrType, nullable);
            return new AggProperty(name, edmTypeRef, isGroup);
        }
        public OeEntryFactory CreateEntryFactory(Type entityType, IEdmEntitySetBase entitySet, Type sourceType)
        {
            OePropertyAccessor[] accessors;
            if (_aggProperties.Count == 0)
                accessors = OePropertyAccessor.CreateFromType(entityType, entitySet);
            else
                accessors = OePropertyAccessor.CreateFromTuple(sourceType, _aggProperties, 0);
            return OeEntryFactory.CreateEntryFactory(entitySet, accessors);
        }
        private OeQueryNodeVisitor CreateVisitor(ParameterExpression parameter)
        {
            return new OeQueryNodeVisitor(_visitor, parameter);
        }
        internal Expression TuplePropertyMapper(Expression source, String aliasName)
        {
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
                        propertyInfo = source.Type.GetTypeInfo().GetProperty("Item1");
                        source = Expression.Property(source, propertyInfo);
                        itemIndex = groupCount;
                    }
                    else
                        itemIndex = i - groupCount + 2;

                    String propertyName = "Item" + itemIndex.ToString();
                    propertyInfo = source.Type.GetTypeInfo().GetProperty(propertyName);
                    return Expression.Property(source, propertyInfo);
                }
            }
            throw new InvalidOperationException("property name " + aliasName + " not found");
        }
    }
}
