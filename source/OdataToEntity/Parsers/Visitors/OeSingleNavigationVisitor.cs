using Microsoft.OData.Edm;
using OdataToEntity.Parsers.Translators;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace OdataToEntity.Parsers
{
    public sealed class OeSingleNavigationVisitor : ExpressionVisitor
    {
        private readonly IEdmModel _edmModel;

        public OeSingleNavigationVisitor(IEdmModel edmModel)
        {
            _edmModel = edmModel;
        }

        private MethodCallExpression? GetJoin(Expression outer, MemberExpression navigationProperty)
        {
            Type outerType = outer.Type;
            Type innerType = navigationProperty.Type;

            IEdmModel? edmModel = _edmModel.GetEdmModel(outerType);
            if (edmModel == null)
                return null;

            Db.OeDataAdapter dataAdapter = edmModel.GetDataAdapter(edmModel.EntityContainer);

            Db.OeEntitySetAdapter? outerEntitySetAdapter = dataAdapter.EntitySetAdapters.Find(outerType);
            if (outerEntitySetAdapter == null)
                throw new InvalidOperationException("OeEntitySetAdapter not found for type " + outerType.Name);

            IEdmEntitySet outerEntitySet = OeEdmClrHelper.GetEntitySet(_edmModel, outerEntitySetAdapter.EntitySetName);
            IEdmNavigationProperty edmNavigationProperty = outerEntitySet.EntityType().NavigationProperties().Single(p => p.Name == navigationProperty.Member.Name);

            Db.OeEntitySetAdapter? innerEntitySetAdapter = dataAdapter.EntitySetAdapters.Find(innerType);
            if (innerEntitySetAdapter == null)
                throw new InvalidOperationException("OeEntitySetAdapter not found for type " + innerType.Name);

            IEdmEntitySet entitySet = edmModel.FindDeclaredEntitySet(innerEntitySetAdapter.EntitySetName);
            ConstantExpression innerSource = OeEnumerableStub.CreateEnumerableStubExpression(innerType, entitySet);

            return GetJoin(outer, innerSource, edmNavigationProperty);
        }
        private static MethodCallExpression GetJoin(Expression outerSource, Expression innerSource, IEdmNavigationProperty navigationProperty)
        {
            Type innerType = OeExpressionHelper.GetCollectionItemType(innerSource.Type);

            var joinBuilder = new OeJoinBuilder(new OeQueryNodeVisitor(Expression.Parameter(typeof(Object))));
            (LambdaExpression outerKeySelector, LambdaExpression innerKeySelector) =
                joinBuilder.GetJoinKeySelector(outerSource.Type, innerType, Array.Empty<IEdmNavigationProperty>(), navigationProperty);

            var replaceParameterVisitor = new ReplaceParameterVisitor(outerSource, outerKeySelector.Parameters[0]);
            Expression outerKeyExpression = replaceParameterVisitor.Visit(outerKeySelector.Body);
            IReadOnlyList<MemberExpression> outerKeyProperties;
            IReadOnlyList<MemberExpression> innerKeyProperties;
            if (OeExpressionHelper.IsTupleType(outerKeySelector.ReturnType))
            {
                outerKeyProperties = OeExpressionHelper.GetPropertyExpressions(outerKeyExpression);
                innerKeyProperties = OeExpressionHelper.GetPropertyExpressions(innerKeySelector.Body);
            }
            else
            {
                outerKeyProperties = new MemberExpression[] { (MemberExpression)outerKeyExpression };
                innerKeyProperties = new MemberExpression[] { (MemberExpression)innerKeySelector.Body };
            }

            BinaryExpression? joinExpression = null;
            for (int i = 0; i < outerKeyProperties.Count; i++)
                if (joinExpression == null)
                    joinExpression = Expression.MakeBinary(ExpressionType.Equal, outerKeyProperties[i], innerKeyProperties[i]);
                else
                {
                    BinaryExpression equal = Expression.MakeBinary(ExpressionType.Equal, outerKeyProperties[i], innerKeyProperties[i]);
                    joinExpression = Expression.MakeBinary(ExpressionType.AndAlso, joinExpression, equal);
                }

            MethodInfo firstMethodInfo = OeMethodInfoHelper.GetFirstMethodInfo(innerType);
            LambdaExpression joinLambda = Expression.Lambda(joinExpression!, innerKeySelector.Parameters);
            return Expression.Call(firstMethodInfo, innerSource, joinLambda);
        }
        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Expression != null && !node.Type.IsEnum && ModelBuilder.PrimitiveTypeHelper.GetPrimitiveType(node.Type) == null)
            {
                IEdmModel? edmModel = _edmModel.GetEdmModel(node.Type);
                if (edmModel != null)
                {
                    MethodCallExpression? join = GetJoin(node.Expression, node);
                    if (join != null)
                        return base.VisitMethodCall(join);
                }
            }

            return base.VisitMember(node);
        }
    }
}
