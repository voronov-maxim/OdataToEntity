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
        private readonly IEdmModel _edmModel;
        private readonly List<IEdmNavigationProperty[]> _groupJoinPaths;

        public OeGroupJoinExpressionBuilder(IEdmModel edmModel)
        {
            _edmModel = edmModel;
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
                Expression innerBody = CoerceJoin(groupJoinOuterKeySelector.Body, groupJoinInnerKeySelector.Body);
                groupJoinInnerKeySelector = Expression.Lambda(innerBody, groupJoinInnerKeySelector.Parameters[0]);
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
        private static Expression CoerceJoin(Expression outer, Expression inner)
        {
            if (outer is NewExpression outerNewExpression)
                return CoerceTupleType(outerNewExpression, (NewExpression)inner);
            return Expression.Convert(inner, inner.Type);
        }
        private static NewExpression CoerceTupleType(NewExpression outer, NewExpression inner)
        {
            var arguments = new Expression[outer.Arguments.Count];
            if (arguments.Length == 8)
            {
                for (int i = 0; i < arguments.Length - 1; i++)
                    arguments[i] = inner.Arguments[i];
                arguments[7] = CoerceTupleType((NewExpression)outer.Arguments[7], (NewExpression)inner.Arguments[7]);
            }

            for (int i = 0; i < arguments.Length; i++)
                if (outer.Arguments[i].Type == inner.Arguments[i].Type)
                    arguments[i] = inner.Arguments[i];
                else
                    arguments[i] = Expression.Convert(inner.Arguments[i], outer.Arguments[i].Type);

            return Expression.New(outer.Type.GetConstructors()[0], arguments, outer.Type.GetProperties());
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
        private static IReadOnlyList<IEdmNavigationProperty> GetGroupJoinPath(OrderByClause orderByClause)
        {
            var propertyNode = (SingleValuePropertyAccessNode)orderByClause.Expression;
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
        public MemberExpression GetGroupJoinPropertyExpression(Expression source, Expression parameter, OrderByClause orderByClause)
        {
            IReadOnlyList<IEdmNavigationProperty> groupJoinPath = GetGroupJoinPath(orderByClause);
            Expression propertyExpression = GetGroupJoinPropertyExpression(parameter, groupJoinPath);
            if (propertyExpression != null)
            {
                var propertyNode = (SingleValuePropertyAccessNode)orderByClause.Expression;
                PropertyInfo propertyInfo = OeEdmClrHelper.GetPropertyIgnoreCase(propertyExpression.Type, propertyNode.Property.Name);
                if (propertyInfo != null)
                    return Expression.Property(propertyExpression, propertyInfo);

                if (OePropertyTranslator.IsPrimitiveType(propertyExpression.Type))
                    return (MemberExpression)propertyExpression;

                var propertyTranslator = new OePropertyTranslator(source);
                return propertyTranslator.CreatePropertyExpression(propertyExpression, propertyNode.Property);
            }

            var visitor = new OeQueryNodeVisitor(_edmModel, (ParameterExpression)parameter);
            propertyExpression = visitor.TranslateNode(orderByClause.Expression);
            if (propertyExpression != null)
                return (MemberExpression)propertyExpression;

            throw new InvalidOperationException("Order by property not found");
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
    }
}
