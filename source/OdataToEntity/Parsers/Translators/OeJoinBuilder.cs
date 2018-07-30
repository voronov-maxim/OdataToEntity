using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace OdataToEntity.Parsers.Translators
{
    public sealed class OeJoinBuilder
    {
        private sealed class ReplaceParameterVisitor : ExpressionVisitor
        {
            private readonly ParameterExpression _parameter;
            private readonly Expression _source;

            public ReplaceParameterVisitor(ParameterExpression parameter, Expression source)
            {
                _parameter = parameter;
                _source = source;
            }

            protected override Expression VisitParameter(ParameterExpression node)
            {
                return node == _parameter ? _source : base.VisitParameter(node);
            }
        }

        private readonly List<IEdmNavigationProperty[]> _joinPaths;

        public OeJoinBuilder(OeQueryNodeVisitor visitor)
        {
            Visitor = visitor;
            _joinPaths = new List<IEdmNavigationProperty[]>();
        }

        public MethodCallExpression Build(Expression outerSource, Expression innerSource, IReadOnlyList<IEdmNavigationProperty> joinPath, IEdmNavigationProperty navigationProperty)
        {
            Type outerType = OeExpressionHelper.GetCollectionItemType(outerSource.Type);
            Type innerType = OeExpressionHelper.GetCollectionItemType(innerSource.Type);

            LambdaExpression groupJoinOuterKeySelector = GetGroupJoinOuterKeySelector(outerType, navigationProperty, joinPath);
            LambdaExpression groupJoinInnerKeySelector = GetGroupJoinInnerKeySelector(innerType, navigationProperty);
            if (groupJoinOuterKeySelector.ReturnType != groupJoinInnerKeySelector.ReturnType)
            {
                Expression innerBody = CoerceExpression(groupJoinOuterKeySelector.Body, groupJoinInnerKeySelector.Body);
                if (innerBody.Type != groupJoinInnerKeySelector.ReturnType)
                    groupJoinInnerKeySelector = Expression.Lambda(innerBody, groupJoinInnerKeySelector.Parameters[0]);

                if (innerBody.Type != groupJoinOuterKeySelector.ReturnType)
                {
                    Expression outerBody = CoerceExpression(innerBody, groupJoinOuterKeySelector.Body);
                    groupJoinOuterKeySelector = Expression.Lambda(outerBody, groupJoinOuterKeySelector.Parameters[0]);
                }
            }

            LambdaExpression groupJoinResultSelect = GetGroupJoinResultSelector(outerType, innerType);

            MethodInfo groupJoinMethodInfo = OeMethodInfoHelper.GetGroupJoinMethodInfo(outerType, innerType, groupJoinOuterKeySelector.ReturnType, groupJoinResultSelect.ReturnType);
            MethodCallExpression groupJoinCall = Expression.Call(groupJoinMethodInfo, outerSource, innerSource, groupJoinOuterKeySelector, groupJoinInnerKeySelector, groupJoinResultSelect);

            LambdaExpression selectManySource = GetSelectManyCollectionSelector(groupJoinResultSelect);
            LambdaExpression selectManyResultSelector = GetSelectManyResultSelector(groupJoinResultSelect);

            MethodInfo selectManyMethodInfo = OeMethodInfoHelper.GetSelectManyMethodInfo(groupJoinResultSelect.ReturnType, innerType, selectManyResultSelector.ReturnType);
            outerSource = Expression.Call(selectManyMethodInfo, groupJoinCall, selectManySource, selectManyResultSelector);

            var newJoinPath = new IEdmNavigationProperty[joinPath.Count + 1];
            for (int i = 0; i < newJoinPath.Length - 1; i++)
                newJoinPath[i] = joinPath[i];
            newJoinPath[newJoinPath.Length - 1] = navigationProperty;
            _joinPaths.Add(newJoinPath);

            return (MethodCallExpression)outerSource;
        }
        private static Expression CoerceExpression(Expression ethalon, Expression coercion)
        {
            if (ethalon is NewExpression outerNewExpression)
                return CoerceTupleType(outerNewExpression, (NewExpression)coercion);
            return Expression.Convert(coercion, coercion.Type);
        }
        private static NewExpression CoerceTupleType(NewExpression ethalon, NewExpression coercion)
        {
            var arguments = new Expression[ethalon.Arguments.Count];
            if (arguments.Length == 8)
            {
                for (int i = 0; i < arguments.Length - 1; i++)
                    arguments[i] = coercion.Arguments[i];
                arguments[7] = CoerceTupleType((NewExpression)ethalon.Arguments[7], (NewExpression)coercion.Arguments[7]);
            }

            bool isCoerced = false;
            for (int i = 0; i < arguments.Length; i++)
                if (ethalon.Arguments[i].Type != coercion.Arguments[i].Type && OeExpressionHelper.IsNullable(ethalon.Arguments[i]))
                {
                    isCoerced = true;
                    arguments[i] = Expression.Convert(coercion.Arguments[i], ethalon.Arguments[i].Type);
                }
                else
                    arguments[i] = coercion.Arguments[i];

            return isCoerced ? Expression.New(ethalon.Type.GetConstructors()[0], arguments, ethalon.Type.GetProperties()) : coercion;
        }
        private static bool CompareJoinPath(IEdmNavigationProperty[] joinPath1, IReadOnlyList<IEdmNavigationProperty> joinPath2)
        {
            if (joinPath1.Length != joinPath2.Count)
                return false;

            for (int i = 0; i < joinPath1.Length; i++)
                if (joinPath1[i] != joinPath2[i])
                    return false;

            return true;
        }
        private int FindJoinPathIndex(IReadOnlyList<IEdmNavigationProperty> joinPath)
        {
            for (int i = 0; i < _joinPaths.Count; i++)
                if (CompareJoinPath(_joinPaths[i], joinPath))
                    return i;

            return -1;
        }
        private static LambdaExpression GetGroupJoinInnerKeySelector(Type innerType, IEdmNavigationProperty edmNavigationProperty)
        {
            IEnumerable<IEdmStructuralProperty> structuralProperties;
            if (edmNavigationProperty.IsPrincipal())
                structuralProperties = edmNavigationProperty.Partner.DependentProperties();
            else
            {
                if (edmNavigationProperty.Partner == null)
                {
                    var structuralPropertyList = new List<IEdmStructuralProperty>();
                    foreach (EdmReferentialConstraintPropertyPair constraintPropertyPair in edmNavigationProperty.ReferentialConstraint.PropertyPairs)
                        structuralPropertyList.Add(constraintPropertyPair.DependentProperty);
                    structuralProperties = structuralPropertyList;
                }
                else
                    structuralProperties = edmNavigationProperty.PrincipalProperties();
            }

            ParameterExpression parameter = Expression.Parameter(innerType, innerType.Name);
            Expression keySelector = GetGroupJoinKeySelector(parameter, structuralProperties);
            return Expression.Lambda(keySelector, parameter);
        }
        private static Expression GetGroupJoinKeySelector(Expression instance, IEnumerable<IEdmStructuralProperty> structuralProperties)
        {
            var expressions = new List<MemberExpression>();
            foreach (IEdmStructuralProperty edmProperty in structuralProperties)
            {
                PropertyInfo clrProperty = instance.Type.GetPropertyIgnoreCase(edmProperty);
                if (clrProperty != null)
                    expressions.Add(Expression.Property(instance, clrProperty));
            }

            if (expressions.Count == 0)
            {
                instance = OeExpressionHelper.GetPropertyExpressions(instance)[0];
                return GetGroupJoinKeySelector(instance, structuralProperties);
            }

            if (expressions.Count == 1)
                return expressions[0];

            return OeExpressionHelper.CreateTupleExpression(expressions);
        }
        private LambdaExpression GetGroupJoinOuterKeySelector(Type outerType, IEdmNavigationProperty edmNavigationProperty, IReadOnlyList<IEdmNavigationProperty> joinPath)
        {
            IEnumerable<IEdmStructuralProperty> structuralProperties;
            if (edmNavigationProperty.IsPrincipal())
                structuralProperties = edmNavigationProperty.Partner.PrincipalProperties();
            else
            {
                if (edmNavigationProperty.Partner == null)
                {
                    var structuralPropertyList = new List<IEdmStructuralProperty>();
                    foreach (EdmReferentialConstraintPropertyPair constraintPropertyPair in edmNavigationProperty.ReferentialConstraint.PropertyPairs)
                        structuralPropertyList.Add(constraintPropertyPair.PrincipalProperty);
                    structuralProperties = structuralPropertyList;
                }
                else
                    structuralProperties = edmNavigationProperty.DependentProperties();
            }

            ParameterExpression outerParameter = Expression.Parameter(outerType, outerType.Name);
            Expression instance = GetJoinPropertyExpression(outerParameter, joinPath);
            if (instance == null)
                throw new InvalidOperationException("Not found group join path + " + edmNavigationProperty.DeclaringType.FullTypeName() + "." + edmNavigationProperty.Name);

            Expression keySelector = GetGroupJoinKeySelector(instance, structuralProperties);
            return Expression.Lambda(keySelector, outerParameter);
        }
        private static LambdaExpression GetGroupJoinResultSelector(Type outerType, Type innerType)
        {
            var parameterExpressions = new ParameterExpression[]
            {
                Expression.Parameter(outerType, "outer"),
                Expression.Parameter(typeof(IEnumerable<>).MakeGenericType(innerType), "inner")
            };

            var expressions = new Expression[]
            {
                parameterExpressions[0],
                Expression.Call(OeMethodInfoHelper.GetDefaultIfEmptyMethodInfo(innerType), parameterExpressions[1])
            };

            NewExpression newTupleExpression = OeExpressionHelper.CreateTupleExpression(expressions);
            return Expression.Lambda(newTupleExpression, parameterExpressions);
        }
        private static IReadOnlyList<IEdmNavigationProperty> GetJoinPath(SingleValuePropertyAccessNode propertyNode)
        {
            if (propertyNode.Source is SingleNavigationNode navigationNode)
            {
                var joinPath = new List<IEdmNavigationProperty>();
                do
                    joinPath.Insert(0, navigationNode.NavigationProperty);
                while ((navigationNode = navigationNode.Source as SingleNavigationNode) != null);
                return joinPath;
            }
            return Array.Empty<IEdmNavigationProperty>();
        }
        public MemberExpression GetJoinPropertyExpression(Expression source, Expression parameter, SingleValuePropertyAccessNode propertyNode)
        {
            IReadOnlyList<IEdmNavigationProperty> joinPath = GetJoinPath(propertyNode);
            MemberExpression propertyExpression = GetJoinPropertyExpression(source, parameter, joinPath, propertyNode.Property);
            if (propertyExpression != null)
                return propertyExpression;

            propertyExpression = (MemberExpression)Visitor.TranslateNode(propertyNode);
            if (propertyExpression == null)
                throw new InvalidOperationException("property " + propertyNode.Property.Name + " not found");

            if (Visitor.Parameter == parameter)
                return propertyExpression;

            var replaceParameterVisitor = new ReplaceParameterVisitor(Visitor.Parameter, parameter);
            return (MemberExpression)replaceParameterVisitor.Visit(propertyExpression);
        }
        private MemberExpression GetJoinPropertyExpression(Expression source, Expression parameter, IReadOnlyList<IEdmNavigationProperty> joinPath, IEdmProperty edmProperty)
        {
            Expression propertyExpression = GetJoinPropertyExpression(parameter, joinPath);
            if (propertyExpression == null)
                return null;

            PropertyInfo propertyInfo = propertyExpression.Type.GetPropertyIgnoreCase(edmProperty);
            if (propertyInfo != null)
                return Expression.Property(propertyExpression, propertyInfo);

            if (OeExpressionHelper.IsPrimitiveType(propertyExpression.Type))
                return (MemberExpression)propertyExpression;

            var propertyTranslator = new OePropertyTranslator(source);
            return propertyTranslator.Build(propertyExpression, edmProperty);
        }
        private Expression GetJoinPropertyExpression(Expression source, IReadOnlyList<IEdmNavigationProperty> joinPath)
        {
            if (joinPath.Count == 0)
            {
                if (_joinPaths.Count > 0 && OeExpressionHelper.IsTupleType(source.Type))
                    return Expression.Property(source, nameof(Tuple<Object>.Item1));

                return source;
            }

            int joinPathIndex = FindJoinPathIndex(joinPath);
            if (joinPathIndex == -1)
                return null;

            IReadOnlyList<MemberExpression> tuplePropertyExpressions = OeExpressionHelper.GetPropertyExpressions(source);
            return tuplePropertyExpressions[joinPathIndex + 1];
        }
        private static LambdaExpression GetSelectManyCollectionSelector(LambdaExpression groupJoinResultSelect)
        {
            ParameterExpression parameter = Expression.Parameter(groupJoinResultSelect.ReturnType);
            MemberExpression inner = Expression.Property(parameter, groupJoinResultSelect.ReturnType.GetProperties()[1]);
            return Expression.Lambda(inner, parameter);
        }
        private static LambdaExpression GetSelectManyResultSelector(LambdaExpression groupJoinResultSelect)
        {
            PropertyInfo outerProperty = groupJoinResultSelect.ReturnType.GetProperty(nameof(Tuple<Object, Object>.Item1));
            PropertyInfo innerProperty = groupJoinResultSelect.ReturnType.GetProperty(nameof(Tuple<Object, Object>.Item2));

            ParameterExpression sourceParameter = Expression.Parameter(groupJoinResultSelect.ReturnType, "source");
            ParameterExpression collectionParameter = Expression.Parameter(OeExpressionHelper.GetCollectionItemType(innerProperty.PropertyType), "collection");

            Expression[] resultPropertyExpressions;
            MemberExpression outerPropertyExpression = Expression.Property(sourceParameter, outerProperty);
            if (OeExpressionHelper.IsTupleType(outerProperty.PropertyType))
            {
                IReadOnlyList<Expression> outerPropertyExpressions = OeExpressionHelper.GetPropertyExpressions(outerPropertyExpression);
                resultPropertyExpressions = new Expression[outerPropertyExpressions.Count + 1];
                for (int i = 0; i < resultPropertyExpressions.Length - 1; i++)
                    resultPropertyExpressions[i] = outerPropertyExpressions[i];
            }
            else
                resultPropertyExpressions = new Expression[] { outerPropertyExpression, null };
            resultPropertyExpressions[resultPropertyExpressions.Length - 1] = collectionParameter;

            NewExpression newTupleExpression = OeExpressionHelper.CreateTupleExpression(resultPropertyExpressions);
            return Expression.Lambda(newTupleExpression, sourceParameter, collectionParameter);
        }

        public OeQueryNodeVisitor Visitor { get; }
    }
}
