using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace OdataToEntity.Parsers.Translators
{
    public readonly struct OeCrossApplyBuilder
    {
        private readonly IEdmModel _edmModel;
        private readonly OeExpressionBuilder _expressionBuilder;

        public OeCrossApplyBuilder(IEdmModel edmModel, OeExpressionBuilder expressionBuilder)
        {
            _edmModel = edmModel;
            _expressionBuilder = expressionBuilder;
        }

        public MethodCallExpression Build(Expression outer, Expression inner, ODataPath odataPath, OrderByClause orderBy, long? skip, long? top)
        {
            var segment = (NavigationPropertySegment)odataPath.LastSegment;
            IEdmNavigationProperty navigationProperty = segment.NavigationProperty;
            if (navigationProperty.ContainsTarget)
            {
                ModelBuilder.ManyToManyJoinDescription joinDescription = _edmModel.GetManyToManyJoinDescription(navigationProperty);
                IEdmEntitySet joinEntitySet = OeEdmClrHelper.GetEntitySet(_edmModel, joinDescription.JoinNavigationProperty);
                outer = OeEnumerableStub.CreateEnumerableStubExpression(joinDescription.JoinClassType, joinEntitySet);
                navigationProperty = joinDescription.TargetNavigationProperty;
            }

            Type outerType = OeExpressionHelper.GetCollectionItemType(outer.Type);
            var outerParameter = Expression.Parameter(outerType, outerType.Name);
            Expression subquery = CreateWhereExpression(outerParameter, inner, navigationProperty);
            subquery = _expressionBuilder.ApplyOrderBy(subquery, orderBy);
            subquery = _expressionBuilder.ApplySkip(subquery, skip, odataPath);
            subquery = _expressionBuilder.ApplyTake(subquery, top, odataPath);

            Type innerType = OeExpressionHelper.GetCollectionItemType(inner.Type);
            MethodInfo selectManyMethdoInfo = OeMethodInfoHelper.GetSelectManyMethodInfo(outerType, innerType);
            return Expression.Call(selectManyMethdoInfo, outer, Expression.Lambda(subquery, outerParameter));
        }
        private static MethodCallExpression CreateWhereExpression(ParameterExpression sourceParameter, Expression subquery, IEdmNavigationProperty edmNavigationProperty)
        {
            Type subqueryType = OeExpressionHelper.GetCollectionItemType(subquery.Type);
            var subqueryParameter = Expression.Parameter(subqueryType, subqueryType.Name);
            BinaryExpression joinExpression = GetJoinExpression(sourceParameter, subqueryParameter, edmNavigationProperty);
            LambdaExpression predicate = Expression.Lambda(joinExpression, subqueryParameter);

            MethodInfo whereMethodInfo = OeMethodInfoHelper.GetWhereMethodInfo(subqueryType);
            return Expression.Call(whereMethodInfo, subquery, predicate);
        }
        private static BinaryExpression GetJoinExpression(ParameterExpression sourceParameter, ParameterExpression subqueryParameter, IEdmNavigationProperty edmNavigationProperty)
        {
            IEnumerable<IEdmStructuralProperty> sourceProperties;
            IEnumerable<IEdmStructuralProperty> subqueryProperties;
            if (edmNavigationProperty.IsPrincipal())
            {
                sourceProperties = edmNavigationProperty.Partner.PrincipalProperties();
                subqueryProperties = edmNavigationProperty.Partner.DependentProperties();
            }
            else
            {
                if (edmNavigationProperty.Type.IsCollection())
                {
                    sourceProperties = edmNavigationProperty.PrincipalProperties();
                    subqueryProperties = edmNavigationProperty.DependentProperties();
                }
                else
                {
                    sourceProperties = edmNavigationProperty.DependentProperties();
                    subqueryProperties = edmNavigationProperty.PrincipalProperties();
                }
            }

            BinaryExpression? joinExpression = null;
            IEnumerator<IEdmStructuralProperty>? sourceEnumerator = null;
            IEnumerator<IEdmStructuralProperty>? subqueryEnumerator = null;
            try
            {
                sourceEnumerator = sourceProperties.GetEnumerator();
                subqueryEnumerator = subqueryProperties.GetEnumerator();
                while (sourceEnumerator.MoveNext())
                {
                    subqueryEnumerator.MoveNext();

                    IEdmStructuralProperty sourceKeyEdmProperty = sourceEnumerator.Current;
                    IEdmStructuralProperty subqueryKeyEdmProperty = subqueryEnumerator.Current;

                    PropertyInfo sourceKeyClrProperty = sourceParameter.Type.GetPropertyIgnoreCase(sourceKeyEdmProperty);
                    PropertyInfo subqueryKeyClrProperty = subqueryParameter.Type.GetPropertyIgnoreCase(subqueryKeyEdmProperty);

                    Expression sourceKeyExpression = Expression.Property(sourceParameter, sourceKeyClrProperty);
                    Expression subqueryKeyExpression = Expression.Property(subqueryParameter, subqueryKeyClrProperty);
                    if (sourceKeyExpression.Type != subqueryKeyExpression.Type)
                        subqueryKeyExpression = Expression.Convert(subqueryKeyExpression, sourceKeyExpression.Type);

                    BinaryExpression equalsExpression = Expression.Equal(sourceKeyExpression, subqueryKeyExpression);
                    joinExpression = joinExpression == null ? equalsExpression : Expression.AndAlso(joinExpression, equalsExpression);
                }
            }
            finally
            {
                if (sourceEnumerator != null)
                    sourceEnumerator.Dispose();
                if (subqueryEnumerator != null)
                    subqueryEnumerator.Dispose();
            }

            return joinExpression!;
        }
    }
}
