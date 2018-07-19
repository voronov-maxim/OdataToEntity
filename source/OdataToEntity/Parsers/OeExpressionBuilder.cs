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
        private Func<Type, IEdmEntitySet, Type, OeEntryFactory> _entryFactory;
        private readonly Translators.OeGroupJoinExpressionBuilder _groupJoinBuilder;

        public OeExpressionBuilder(Translators.OeGroupJoinExpressionBuilder groupJoinBuilder)
        {
            _groupJoinBuilder = groupJoinBuilder;
            Visitor = groupJoinBuilder.Visitor;
        }
        public OeExpressionBuilder(Translators.OeGroupJoinExpressionBuilder groupJoinBuilder, OeQueryNodeVisitor visitor)
        {
            _groupJoinBuilder = groupJoinBuilder;
            Visitor = visitor;
        }

        public Expression ApplyAggregation(Expression source, ApplyClause applyClause)
        {
            if (applyClause == null)
                return source;

            var aggTranslator = new Translators.OeAggregationTranslator(Visitor);
            Expression aggExpression = aggTranslator.Build(source, applyClause);

            _entryFactory = aggTranslator.CreateEntryFactory;

            Visitor.ChangeParameterType(aggExpression);
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
        public Expression ApplyNavigation(Expression source, IEnumerable<OeParseNavigationSegment> parseNavigationSegments)
        {
            if (parseNavigationSegments == null)
                return source;

            Type sourceItemType = OeExpressionHelper.GetCollectionItemType(source.Type);
            foreach (OeParseNavigationSegment parseNavigationSegment in parseNavigationSegments)
            {
                Type selectType;
                ParameterExpression parameter;
                Expression e;
                if (parseNavigationSegment.NavigationSegment == null) //EntitySetSegment
                {
                    parameter = Visitor.Parameter;
                    e = source;
                    selectType = sourceItemType;
                }
                else
                {
                    parameter = Expression.Parameter(sourceItemType);
                    PropertyInfo navigationClrProperty = sourceItemType.GetProperty(parseNavigationSegment.NavigationSegment.NavigationProperty.Name);
                    e = Expression.MakeMemberAccess(parameter, navigationClrProperty);

                    MethodInfo selectMethodInfo;
                    selectType = OeExpressionHelper.GetCollectionItemType(e.Type);
                    if (selectType == null)
                    {
                        selectType = e.Type;
                        selectMethodInfo = OeMethodInfoHelper.GetSelectMethodInfo(sourceItemType, selectType);
                    }
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

            Visitor.ChangeParameterType(source);

            var orderBytranslator = new Translators.OeOrderByTranslator(Visitor, _groupJoinBuilder);
            return orderBytranslator.Build(source, orderByClause);
        }
        public Expression ApplySelect(Expression source, OeQueryContext queryContext)
        {
            if (queryContext.ODataUri.SelectAndExpand == null && (queryContext.ODataUri.OrderBy == null || queryContext.PageSize == 0))
                return source;

            var selectTranslator = new Translators.OeSelectTranslator(Visitor, queryContext.ODataUri.Path);
            source = selectTranslator.Build(source, queryContext);
            _entryFactory = selectTranslator.CreateEntryFactory;

            Visitor.ChangeParameterType(source);
            return source;
        }
        public Expression ApplySkip(Expression source, long? skip, ODataPath path)
        {
            if (skip == null)
                return source;

            ConstantExpression skipConstant = Expression.Constant((int)skip.Value, typeof(int));
            Visitor.AddSkipConstant(skipConstant, path);

            MethodInfo skipMethodInfo = OeMethodInfoHelper.GetSkipMethodInfo(ParameterType);
            return Expression.Call(skipMethodInfo, source, skipConstant);
        }
        public Expression ApplySkipToken(Expression source, OeSkipTokenNameValue[] skipTokenNameValues, OrderByClause uniqueOrderBy, bool isDatabaseNullHighestValue)
        {
            if (skipTokenNameValues == null || skipTokenNameValues.Length == 0)
                return source;

            Visitor.ChangeParameterType(source);

            var skipTokenTranslator = new Translators.OeSkipTokenTranslator(Visitor, _groupJoinBuilder, isDatabaseNullHighestValue);
            return skipTokenTranslator.Build(source, skipTokenNameValues, uniqueOrderBy);
        }
        public Expression ApplyTake(Expression source, long? top, ODataPath path)
        {
            if (top == null)
                return source;

            ConstantExpression topConstant = Expression.Constant((int)top.Value, typeof(int));
            Visitor.AddTopConstant(topConstant, path);

            MethodInfo takeMethodInfo = OeMethodInfoHelper.GetTakeMethodInfo(ParameterType);
            return Expression.Call(takeMethodInfo, source, topConstant);
        }
        public OeEntryFactory CreateEntryFactory(IEdmEntitySet entitySet)
        {
            if (_entryFactory != null)
            {
                var zzz = OeEdmClrHelper.GetClrType(Visitor.EdmModel, entitySet.EntityType());
                return _entryFactory(zzz, entitySet, ParameterType);
            }

            OePropertyAccessor[] accessors = OePropertyAccessor.CreateFromType(ParameterType, entitySet);
            return OeEntryFactory.CreateEntryFactory(entitySet, accessors);
        }

        public IReadOnlyDictionary<ConstantExpression, ConstantNode> Constants => Visitor.Constans;
        private Type ParameterType => Visitor.Parameter.Type;
        private OeQueryNodeVisitor Visitor { get; }
    }
}
