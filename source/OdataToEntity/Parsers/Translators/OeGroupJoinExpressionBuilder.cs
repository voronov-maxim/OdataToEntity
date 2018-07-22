using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace OdataToEntity.Parsers.Translators
{
    public sealed class OeGroupJoinExpressionBuilder
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

        private readonly List<IEdmNavigationProperty[]> _groupJoinPaths;

        public OeGroupJoinExpressionBuilder(OeQueryNodeVisitor visitor)
        {
            Visitor = visitor;
            _groupJoinPaths = new List<IEdmNavigationProperty[]>();
        }

        public MethodCallExpression Build(Expression outerSource, Expression innerSource, IReadOnlyList<IEdmNavigationProperty> groupJoinPath, IEdmNavigationProperty navigationProperty)
        {
            Type outerType = OeExpressionHelper.GetCollectionItemType(outerSource.Type);
            Type innerType = OeExpressionHelper.GetCollectionItemType(innerSource.Type);

            LambdaExpression groupJoinOuterKeySelector = GetGroupJoinOuterKeySelector(outerType, navigationProperty, groupJoinPath);
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

            var newGroupJoinPath = new IEdmNavigationProperty[groupJoinPath.Count + 1];
            for (int i = 0; i < newGroupJoinPath.Length - 1; i++)
                newGroupJoinPath[i] = groupJoinPath[i];
            newGroupJoinPath[newGroupJoinPath.Length - 1] = navigationProperty;
            _groupJoinPaths.Add(newGroupJoinPath);

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
        private static bool CompareGroupJoinPath(IEdmNavigationProperty[] groupJoinPath1, IReadOnlyList<IEdmNavigationProperty> groupJoinPath2)
        {
            if (groupJoinPath1.Length != groupJoinPath2.Count)
                return false;

            for (int i = 0; i < groupJoinPath1.Length; i++)
                if (groupJoinPath1[i] != groupJoinPath2[i])
                    return false;

            return true;
        }
        private int FindGroupJoinPathIndex(IReadOnlyList<IEdmNavigationProperty> groupJoinPath)
        {
            for (int i = 0; i < _groupJoinPaths.Count; i++)
                if (CompareGroupJoinPath(_groupJoinPaths[i], groupJoinPath))
                    return i;

            return -1;
        }
        private static LambdaExpression GetGroupJoinInnerKeySelector(Type innerType, IEdmNavigationProperty edmNavigationProperty)
        {
            IEnumerable<IEdmStructuralProperty> structuralProperties = edmNavigationProperty.PrincipalProperties();
            if (structuralProperties == null)
                structuralProperties = edmNavigationProperty.Partner.DependentProperties();

            ParameterExpression parameter = Expression.Parameter(innerType, innerType.Name);
            Expression keySelector = GetGroupJoinKeySelector(parameter, structuralProperties);
            return Expression.Lambda(keySelector, parameter);
        }
        private static Expression GetGroupJoinKeySelector(Expression instance, IEnumerable<IEdmStructuralProperty> structuralProperties)
        {
            var expressions = new List<MemberExpression>();
            foreach (IEdmStructuralProperty edmProperty in structuralProperties)
            {
                PropertyInfo clrProperty = OeEdmClrHelper.GetPropertyIgnoreCase(instance.Type, edmProperty.Name);
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
        private LambdaExpression GetGroupJoinOuterKeySelector(Type outerType, IEdmNavigationProperty navigationProperty, IReadOnlyList<IEdmNavigationProperty> groupJoinPath)
        {
            IEnumerable<IEdmStructuralProperty> structuralProperties = navigationProperty.DependentProperties();
            if (structuralProperties == null)
                structuralProperties = navigationProperty.Partner.PrincipalProperties();

            ParameterExpression outerParameter = Expression.Parameter(outerType, outerType.Name);
            Expression instance = GetGroupJoinPropertyExpression(outerParameter, groupJoinPath);
            if (instance == null)
                throw new InvalidOperationException("Not found group join path + " + navigationProperty.DeclaringType.FullTypeName() + "." + navigationProperty.Name);

            Expression keySelector = GetGroupJoinKeySelector(instance, structuralProperties);
            return Expression.Lambda(keySelector, outerParameter);
        }
        private static IReadOnlyList<IEdmNavigationProperty> GetGroupJoinPath(SingleValuePropertyAccessNode propertyNode)
        {
            if (propertyNode.Source is SingleNavigationNode navigationNode)
            {
                var groupJoinPath = new List<IEdmNavigationProperty>();
                do
                    groupJoinPath.Insert(0, navigationNode.NavigationProperty);
                while ((navigationNode = navigationNode.Source as SingleNavigationNode) != null);
                return groupJoinPath;
            }
            return Array.Empty<IEdmNavigationProperty>();
        }
        public MemberExpression GetGroupJoinPropertyExpression(Expression source, Expression parameter, SingleValuePropertyAccessNode propertyNode)
        {
            IReadOnlyList<IEdmNavigationProperty> groupJoinPath = GetGroupJoinPath(propertyNode);
            MemberExpression propertyExpression = GetGroupJoinPropertyExpression(source, parameter, groupJoinPath, propertyNode.Property);
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
        public MemberExpression GetGroupJoinPropertyExpression(Expression source, Expression parameter, IReadOnlyList<IEdmNavigationProperty> groupJoinPath, IEdmProperty edmProperty)
        {
            Expression propertyExpression = GetGroupJoinPropertyExpression(parameter, groupJoinPath);
            if (propertyExpression == null)
                return null;

            PropertyInfo propertyInfo = OeEdmClrHelper.GetPropertyIgnoreCase(propertyExpression.Type, edmProperty.Name);
            if (propertyInfo != null && OeExpressionHelper.IsPrimitiveType(propertyInfo.PropertyType))
                return Expression.Property(propertyExpression, propertyInfo);

            if (OeExpressionHelper.IsPrimitiveType(propertyExpression.Type))
                return (MemberExpression)propertyExpression;

            var propertyTranslator = new OePropertyTranslator(source);
            return propertyTranslator.Build(propertyExpression, edmProperty);
        }
        private Expression GetGroupJoinPropertyExpression(Expression source, IReadOnlyList<IEdmNavigationProperty> navigationProperties)
        {
            if (navigationProperties.Count == 0)
            {
                if (_groupJoinPaths.Count > 0 && OeExpressionHelper.IsTupleType(source.Type))
                    return Expression.Property(source, nameof(Tuple<Object>.Item1));

                return source;
            }

            int groupJoinPathIndex = FindGroupJoinPathIndex(navigationProperties);
            if (groupJoinPathIndex == -1)
                return null;

            IReadOnlyList<MemberExpression> tuplePropertyExpressions = OeExpressionHelper.GetPropertyExpressions(source);
            return tuplePropertyExpressions[groupJoinPathIndex + 1];
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
