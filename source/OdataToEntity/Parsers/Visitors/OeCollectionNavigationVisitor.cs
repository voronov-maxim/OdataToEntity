using Microsoft.OData.Edm;
using OdataToEntity.Parsers.Translators;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace OdataToEntity.Parsers
{
    public sealed class OeCollectionNavigationVisitor : ExpressionVisitor
    {
        private sealed class JoinVisitor : ExpressionVisitor
        {
            private readonly IEdmModel _edmModel;
            private readonly ParameterExpression _outerParameterExpression;
            private readonly Expression _outerSource;

            public JoinVisitor(IEdmModel edmModel, Expression outerSource, ParameterExpression outerParameterExpression)
            {
                _edmModel = edmModel;
                _outerSource = outerSource;
                _outerParameterExpression = outerParameterExpression;
            }

            private MethodCallExpression? GetJoin(MemberExpression navigationProperty)
            {
                if (navigationProperty.Expression == null)
                    return null;

                Type? innerItemType = OeExpressionHelper.GetCollectionItemTypeOrNull(navigationProperty.Type);
                if (innerItemType == null)
                    return null;

                IEdmModel edmModel = _edmModel.GetEdmModel(innerItemType) ?? throw new InvalidOperationException("Cannot find IEdmModel for type " + innerItemType.FullName);
                Db.OeDataAdapter dataAdapter = edmModel.GetDataAdapter(edmModel.EntityContainer);

                Db.OeEntitySetAdapter? innerEntitySetAdapter = dataAdapter.EntitySetAdapters.Find(innerItemType);
                if (innerEntitySetAdapter == null)
                    throw new InvalidOperationException("OeEntitySetAdapter not found for type " + innerItemType.Name);

                IEdmEntitySet entitySet = edmModel.FindDeclaredEntitySet(innerEntitySetAdapter.EntitySetName);
                ConstantExpression innerSource = OeEnumerableStub.CreateEnumerableStubExpression(innerEntitySetAdapter.EntityType, entitySet);

                Type outerItemType = navigationProperty.Expression!.Type;
                Db.OeEntitySetAdapter? outerEntitySetAdapter = dataAdapter.EntitySetAdapters.Find(outerItemType);
                if (outerEntitySetAdapter == null)
                    throw new InvalidOperationException("OeEntitySetAdapter not found for type " + outerItemType.Name);

                IEdmEntitySet outerEntitySet = OeEdmClrHelper.GetEntitySet(_edmModel, outerEntitySetAdapter.EntitySetName);
                IEdmNavigationProperty edmNavigationProperty = outerEntitySet.EntityType().NavigationProperties().Single(p => p.Name == navigationProperty.Member.Name);

                return GetJoin(innerSource, edmNavigationProperty);
            }
            private MethodCallExpression GetJoin(Expression innerSource, IEdmNavigationProperty navigationProperty)
            {
                Type outerType = OeExpressionHelper.GetCollectionItemType(_outerSource.Type);
                Type innerType = OeExpressionHelper.GetCollectionItemType(innerSource.Type);

                var joinBuilder = new OeJoinBuilder(new OeQueryNodeVisitor(_outerParameterExpression));
                (LambdaExpression outerKeySelector, LambdaExpression innerKeySelector) =
                    joinBuilder.GetJoinKeySelector(outerType, innerType, Array.Empty<IEdmNavigationProperty>(), navigationProperty);

                var replaceParameterVisitor = new ReplaceParameterVisitor(_outerParameterExpression, outerKeySelector.Parameters[0]);
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

                MethodInfo whereMethodInfo = OeMethodInfoHelper.GetWhereMethodInfo(innerType);
                LambdaExpression joinLambda = Expression.Lambda(joinExpression!, innerKeySelector.Parameters);
                return Expression.Call(whereMethodInfo, innerSource, joinLambda);
            }
            protected override Expression VisitMember(MemberExpression node)
            {
                MethodCallExpression? join = GetJoin(node);
                return join ?? base.VisitMember(node);
            }
            protected override Expression VisitMethodCall(MethodCallExpression node)
            {
                if (node.Arguments.Count > 0 && node.Arguments[0] is MemberExpression navigationProperty)
                {
                    MethodCallExpression? join = GetJoin(navigationProperty);
                    if (join != null)
                    {
                        Expression[] arguments = node.Arguments.ToArray();
                        arguments[0] = join;
                        return node.Update(node.Object!, arguments);
                    }
                }

                return base.VisitMethodCall(node);
            }
        }

        private readonly IEdmModel _edmModel;

        public OeCollectionNavigationVisitor(IEdmModel edmModel)
        {
            _edmModel = edmModel;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.Name == nameof(Enumerable.Where))
            {
                var predicate = node.Arguments[1] is UnaryExpression quote ? (LambdaExpression)quote.Operand : (LambdaExpression)node.Arguments[1];
                var joinVisitor = new JoinVisitor(_edmModel, node.Arguments[0], predicate.Parameters[0]);
                Expression expression = joinVisitor.Visit(node.Arguments[1]);
                if (node.Arguments[1] != expression)
                {
                    Expression[] arguments = node.Arguments.ToArray();
                    arguments[1] = expression;
                    return node.Update(node.Object!, arguments);
                }
            }
            else if (node.Method.Name == nameof(Enumerable.SelectMany) && node.Arguments.Count == 2)
            {
                var predicate = node.Arguments[1] is UnaryExpression quote ? (LambdaExpression)quote.Operand : (LambdaExpression)node.Arguments[1];
                var joinVisitor = new JoinVisitor(_edmModel, node.Arguments[0], predicate.Parameters[0]);
                Expression body = joinVisitor.Visit(predicate.Body);
                if (predicate.Body != body)
                {
                    Expression[] arguments = node.Arguments.ToArray();
                    arguments[1] = Expression.Lambda(body, predicate.Parameters);
                    return node.Update(node.Object!, arguments);
                }
            }
            return base.VisitMethodCall(node);
        }

    }
}
