using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using Microsoft.OData.UriParser.Aggregation;
using System;
using System.Linq.Expressions;
using System.Reflection;

namespace OdataToEntity.Parsers
{
    public sealed class OeExpressionBuilder
    {
        private Type _entityType;
        private readonly IEdmModel _model;
        private Func<Type, IEdmEntitySet, Type, OeEntryFactory> _entryFactory;
        private OeQueryNodeVisitor _visitor;

        public OeExpressionBuilder(IEdmModel model, Type entityType)
        {
            _model = model;
            _entityType = entityType;

            _visitor = new OeQueryNodeVisitor(_model, Expression.Parameter(_entityType));
        }

        public MethodCallExpression ApplyAggregation(MethodCallExpression source, ApplyClause applyClause)
        {
            if (applyClause == null)
                return source;

            var aggTranslator = new OeAggregationTranslator(_model);
            MethodCallExpression aggExpression = aggTranslator.Build(source, applyClause);

            _entryFactory = aggTranslator.CreateEntryFactory;

            Type aggItemType = OeExpressionHelper.GetCollectionItemType(aggExpression.Type);
            _visitor = new OeQueryNodeVisitor(_model, Expression.Parameter(aggItemType));
            _visitor.TuplePropertyMapper = aggTranslator.TuplePropertyMapper;

            return aggExpression;
        }
        public MethodCallExpression ApplyCount(MethodCallExpression source)
        {
            MethodInfo countMethodInfo = OeMethodInfoHelper.GetCountMethodInfo(ParameterType);
            return Expression.Call(countMethodInfo, source);
        }
        public MethodCallExpression ApplyFilter(MethodCallExpression source, FilterClause filterClause)
        {
            if (filterClause == null)
                return source;

            Expression e = _visitor.TranslateNode(filterClause.Expression);
            LambdaExpression lambda = Expression.Lambda(e, Parameter);

            MethodInfo whereMethodInfo = OeMethodInfoHelper.GetWhereMethodInfo(ParameterType);
            return Expression.Call(whereMethodInfo, source, lambda);
        }
        public MethodCallExpression ApplyOrderBy(MethodCallExpression source, OrderByClause orderByClause)
        {
            if (orderByClause == null)
                return source;

            Expression e = _visitor.TranslateNode(orderByClause.Expression);
            LambdaExpression lambda = Expression.Lambda(e, Parameter);

            MethodInfo orderByMethodInfo = orderByClause.Direction == OrderByDirection.Ascending ?
                OeMethodInfoHelper.GetOrderByMethodInfo(ParameterType, e.Type) :
                OeMethodInfoHelper.GetOrderByDescendingMethodInfo(ParameterType, e.Type);
            MethodCallExpression orderByCall = Expression.Call(orderByMethodInfo, source, lambda);

            return ApplyThenBy(orderByCall, orderByClause.ThenBy);
        }
        public MethodCallExpression ApplySelect(MethodCallExpression source, SelectExpandClause selectClause)
        {
            if (selectClause == null)
                return source;

            var selectTranslator = new OeSelectTranslator();
            MethodCallExpression selectExpression = selectTranslator.Build(source, selectClause);

            _entryFactory = selectTranslator.CreateEntryFactory;
            if (selectTranslator.LastNavigationType != null)
                _entityType = selectTranslator.LastNavigationType;

            Type selectItemType = OeExpressionHelper.GetCollectionItemType(selectExpression.Type);
            _visitor = new OeQueryNodeVisitor(_model, Expression.Parameter(selectItemType));

            return selectExpression;
        }
        public MethodCallExpression ApplySkip(MethodCallExpression source, long? skip)
        {
            if (skip == null)
                return source;

            MethodInfo skipMethodInfo = OeMethodInfoHelper.GetSkipMethodInfo(ParameterType);
            return Expression.Call(skipMethodInfo, source, Expression.Constant((int)skip.Value));
        }
        public MethodCallExpression ApplyTake(MethodCallExpression source, long? top)
        {
            if (top == null)
                return source;

            MethodInfo takeMethodInfo = OeMethodInfoHelper.GetTakeMethodInfo(ParameterType);
            return Expression.Call(takeMethodInfo, source, Expression.Constant((int)top.Value));
        }
        private MethodCallExpression ApplyThenBy(MethodCallExpression source, OrderByClause thenByClause)
        {
            if (thenByClause == null)
                return source;

            Expression e = _visitor.TranslateNode(thenByClause.Expression);
            LambdaExpression lambda = Expression.Lambda(e, Parameter);

            MethodInfo thenByMethodInfo = thenByClause.Direction == OrderByDirection.Ascending ?
                OeMethodInfoHelper.GetThenByMethodInfo(ParameterType, e.Type) :
                OeMethodInfoHelper.GetThenByDescendingMethodInfo(ParameterType, e.Type);
            MethodCallExpression thenByCall = Expression.Call(thenByMethodInfo, source, lambda);
            return ApplyThenBy(thenByCall, thenByClause.ThenBy);
        }
        public OeEntryFactory CreateEntryFactory(IEdmEntitySet entitySet)
        {
            if (_entryFactory != null)
                return _entryFactory(EntityType, entitySet, ParameterType);

            OePropertyAccessor[] accessors = OePropertyAccessor.CreateFromType(ParameterType, entitySet);
            return OeEntryFactory.CreateEntryFactory(entitySet, accessors);
        }

        public Type EntityType => _entityType;
        private ParameterExpression Parameter => _visitor.Parameter;
        private Type ParameterType => _visitor.Parameter.Type;
    }
}
