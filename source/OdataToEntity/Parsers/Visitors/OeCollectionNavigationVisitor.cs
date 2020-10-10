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

            private MethodCallExpression GetJoin(Expression innerSource, IEdmNavigationProperty navigationProperty)
            {
                Type outerType = OeExpressionHelper.GetCollectionItemType(_outerSource.Type);
                Type innerType = OeExpressionHelper.GetCollectionItemType(innerSource.Type);

                var joinBuilder = new OeJoinBuilder(new OeQueryNodeVisitor(_outerParameterExpression));
                (LambdaExpression outerKeySelector, LambdaExpression innerKeySelector) =
                    joinBuilder.GetJoinKeySelector(outerType, innerType, Array.Empty<IEdmNavigationProperty>(), navigationProperty);

                var replaceParameterVisitor = new ReplaceParameterVisitor(outerKeySelector.Parameters[0], _outerParameterExpression);
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
                {
                    if (joinExpression == null)
                        joinExpression = Expression.MakeBinary(ExpressionType.Equal, outerKeyProperties[i], innerKeyProperties[i]);
                    else
                    {
                        BinaryExpression equal = Expression.MakeBinary(ExpressionType.Equal, outerKeyProperties[i], innerKeyProperties[i]);
                        joinExpression = Expression.MakeBinary(ExpressionType.AndAlso, joinExpression, equal);
                    }
                }

                MethodInfo whereMethodInfo = OeMethodInfoHelper.GetWhereMethodInfo(innerType);
                LambdaExpression joinLambda = Expression.Lambda(joinExpression!, innerKeySelector.Parameters);
                return Expression.Call(whereMethodInfo, innerSource, joinLambda);
            }
            private IEdmNavigationProperty GetEdmNavigationProperty(Db.OeDataAdapter dataAdapter, MemberExpression navigationProperty)
            {
                Type outerItemType = OeExpressionHelper.GetCollectionItemType(_outerSource.Type);
                Db.OeEntitySetAdapter? outerEntitySetAdapter = dataAdapter.EntitySetAdapters.Find(outerItemType);
                if (outerEntitySetAdapter == null)
                    throw new InvalidOperationException("OeEntitySetAdapter not found for type " + outerItemType.Name);
                IEdmEntitySet outerEntitySet = OeEdmClrHelper.GetEntitySet(_edmModel, outerEntitySetAdapter.EntitySetName);
                return outerEntitySet.EntityType().NavigationProperties().Single(p => p.Name == navigationProperty.Member.Name);
            }
            protected override Expression VisitMethodCall(MethodCallExpression node)
            {
                if (node.Arguments.Count > 0 && node.Arguments[0] is MemberExpression navigationPropertyExpression)
                {
                    Type? innerItemType = OeExpressionHelper.GetCollectionItemTypeOrNull(navigationPropertyExpression.Type);
                    if (innerItemType != null)
                    {
                        IEdmModel edmModel = _edmModel.GetEdmModel(innerItemType) ?? throw new InvalidOperationException("Cannot find IEdmModel for type " + innerItemType.FullName);
                        Db.OeDataAdapter dataAdapter = edmModel.GetDataAdapter(edmModel.EntityContainer);

                        Db.OeEntitySetAdapter? innerEntitySetAdapter = dataAdapter.EntitySetAdapters.Find(innerItemType);
                        if (innerEntitySetAdapter == null)
                            throw new InvalidOperationException("OeEntitySetAdapter not found for type " + innerItemType.Name);
                        IEdmEntitySet entitySet = edmModel.FindDeclaredEntitySet(innerEntitySetAdapter.EntitySetName);
                        ConstantExpression innerSource = OeEnumerableStub.CreateEnumerableStubExpression(innerEntitySetAdapter.EntityType, entitySet);

                        IEdmNavigationProperty navigationProperty = GetEdmNavigationProperty(dataAdapter, navigationPropertyExpression);
                        Expression[] arguments = node.Arguments.ToArray();
                        arguments[0] = GetJoin(innerSource, navigationProperty);
                        return node.Update(node.Object!, arguments);
                    }
                }

                return base.VisitMethodCall(node);
            }
        }

        private readonly IEdmModel _edmModel;
        private readonly Expression _expression;

        public OeCollectionNavigationVisitor(IEdmModel edmModel, Expression expression)
        {
            _edmModel = edmModel;
            _expression = expression;
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
            return base.VisitMethodCall(node);
        }

    }
}
