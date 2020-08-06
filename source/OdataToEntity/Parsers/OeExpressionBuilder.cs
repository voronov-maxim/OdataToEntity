using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using Microsoft.OData.UriParser.Aggregation;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace OdataToEntity.Parsers
{
    public sealed class OeExpressionBuilder
    {
        private OeEntryFactoryFactory? _entryFactoryFactory;
        private readonly Translators.OeJoinBuilder _joinBuilder;

        public OeExpressionBuilder(Translators.OeJoinBuilder joinBuilder)
        {
            _joinBuilder = joinBuilder;
            Visitor = joinBuilder.Visitor;
        }
        public OeExpressionBuilder(Translators.OeJoinBuilder joinBuilder, OeQueryNodeVisitor visitor)
        {
            _joinBuilder = joinBuilder;
            Visitor = visitor;
        }

        public Expression ApplyAggregation(Expression source, ApplyClause applyClause)
        {
            if (applyClause == null)
                return source;

            var aggTranslator = new Translators.OeAggregationTranslator(Visitor);
            Expression aggExpression = aggTranslator.Build(source, applyClause, out _entryFactoryFactory);

            ChangeParameterType(aggExpression);
            Visitor.TuplePropertyByAliasName = aggTranslator.GetTuplePropertyByAliasName;

            return aggExpression;
        }
        public Expression ApplyCount(Expression source, bool? queryCount)
        {
            if (!queryCount.GetValueOrDefault())
                return source;

            MethodInfo countMethodInfo = OeMethodInfoHelper.GetCountMethodInfo(ParameterType);
            return Expression.Call(countMethodInfo, source);
        }
        public Expression ApplyFilter(Expression source, FilterClause filterClause)
        {
            if (filterClause == null)
                return source;

            Expression e = Visitor.TranslateNode(filterClause.Expression);
            LambdaExpression lambda = Expression.Lambda(e, Visitor.Parameter);

            MethodInfo whereMethodInfo = OeMethodInfoHelper.GetWhereMethodInfo(ParameterType);
            return Expression.Call(whereMethodInfo, source, lambda);
        }
        public Expression ApplyNavigation(Expression source, IReadOnlyList<OeParseNavigationSegment> parseNavigationSegments)
        {
            if (parseNavigationSegments == null)
                return source;

            Type sourceItemType = OeExpressionHelper.GetCollectionItemType(source.Type);
            foreach (OeParseNavigationSegment parseNavigationSegment in parseNavigationSegments)
            {
                Type selectType;
                ParameterExpression parameter;
                Expression e;
                if (parseNavigationSegment.NavigationSegment == null) //EntitySetSegment, KeySegment
                {
                    parameter = Visitor.Parameter;
                    e = source;
                    selectType = sourceItemType;
                }
                else
                {
                    parameter = Expression.Parameter(sourceItemType);
                    PropertyInfo navigationClrProperty = sourceItemType.GetPropertyIgnoreCase(parseNavigationSegment.NavigationSegment.NavigationProperty);
                    e = Expression.MakeMemberAccess(parameter, navigationClrProperty);

                    MethodInfo selectMethodInfo;
                    selectType = OeExpressionHelper.GetCollectionItemTypeOrNull(e.Type) ?? e.Type;
                    if (selectType == e.Type)
                        selectMethodInfo = OeMethodInfoHelper.GetSelectMethodInfo(sourceItemType, selectType);
                    else
                        selectMethodInfo = OeMethodInfoHelper.GetSelectManyMethodInfo(sourceItemType, selectType);

                    LambdaExpression lambda = Expression.Lambda(e, parameter);
                    source = Expression.Call(selectMethodInfo, source, lambda);
                }

                if (parseNavigationSegment.Filter != null)
                {
                    var visitor = new OeQueryNodeVisitor(Visitor, Expression.Parameter(selectType));
                    e = visitor.TranslateNode(parseNavigationSegment.Filter.Expression);
                    LambdaExpression lambda = Expression.Lambda(e, visitor.Parameter);

                    MethodInfo whereMethodInfo = OeMethodInfoHelper.GetWhereMethodInfo(selectType);
                    source = Expression.Call(whereMethodInfo, source, lambda);
                }

                sourceItemType = selectType;
            }

            Visitor.ChangeParameterType(Expression.Parameter(sourceItemType));
            return source;
        }
        public Expression ApplyOrderBy(Expression source, OrderByClause orderByClause)
        {
            if (orderByClause == null)
                return source;

            return Translators.OeOrderByTranslator.Build(_joinBuilder, source, Visitor.Parameter, orderByClause);
        }
        public Expression ApplySelect(Expression source, OeQueryContext queryContext)
        {
            if (queryContext.ODataUri.Path.LastSegment is CountSegment)
                return source;

            var selectTranslator = new Translators.OeSelectTranslator(queryContext.EdmModel, _joinBuilder, queryContext.ODataUri);
            var selectTranslatorParameters = new Translators.OeSelectTranslatorParameters()
            {
                IsDatabaseNullHighestValue = queryContext.IsDatabaseNullHighestValue,
                MetadataLevel = queryContext.MetadataLevel,
                SkipTokenNameValues = queryContext.SkipTokenNameValues
            };
            source = selectTranslator.Build(source, ref selectTranslatorParameters, out _entryFactoryFactory);

            ChangeParameterType(source);
            return source;
        }
        public Expression ApplySkip(Expression source, long? skip, ODataPath path)
        {
            if (skip == null)
                return source;

            ConstantExpression skipConstant = Visitor.AddSkipConstant((int)skip.Value, path);
            MethodInfo skipMethodInfo = OeMethodInfoHelper.GetSkipMethodInfo(ParameterType);
            return Expression.Call(skipMethodInfo, source, skipConstant);
        }
        public Expression ApplySkipToken(Expression source, OeSkipTokenNameValue[] skipTokenNameValues, OrderByClause uniqueOrderBy, bool isDatabaseNullHighestValue)
        {
            if (skipTokenNameValues == null || skipTokenNameValues.Length == 0)
                return source;

            var skipTokenTranslator = new Translators.OeSkipTokenTranslator(Visitor, _joinBuilder, isDatabaseNullHighestValue);
            source = skipTokenTranslator.Build(source, skipTokenNameValues, uniqueOrderBy);

            Visitor.ChangeParameterType(source);
            return source;
        }
        public Expression ApplyTake(Expression source, long? top, ODataPath path)
        {
            if (top == null)
                return source;

            ConstantExpression topConstant = Visitor.AddTopConstant((int)top.Value, path);
            MethodInfo takeMethodInfo = OeMethodInfoHelper.GetTakeMethodInfo(ParameterType);
            return Expression.Call(takeMethodInfo, source, topConstant);
        }
        private void ChangeParameterType(Expression source)
        {
            _joinBuilder.Visitor.ChangeParameterType(source);
        }
        public OeEntryFactory CreateEntryFactory(IEdmEntitySet entitySet, OePropertyAccessor[]? skipTokenAccessors)
        {
            if (_entryFactoryFactory != null)
                return _entryFactoryFactory.CreateEntryFactory(entitySet, ParameterType, skipTokenAccessors);

            OePropertyAccessor[] accessors = OePropertyAccessor.CreateFromType(ParameterType, entitySet);
            return new OeEntryFactory(entitySet, accessors, skipTokenAccessors);
        }

        public IReadOnlyDictionary<ConstantExpression, ConstantNode> Constants => Visitor.Constans;
        private Type ParameterType => Visitor.Parameter.Type;
        private OeQueryNodeVisitor Visitor { get; }
    }
}
