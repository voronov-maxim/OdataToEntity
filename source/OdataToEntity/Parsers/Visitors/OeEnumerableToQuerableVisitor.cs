using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace OdataToEntity.Parsers
{
    public class OeEnumerableToQuerableVisitor : ExpressionVisitor
    {
        private static readonly Dictionary<String, MethodInfo[]> _queryableMethods =
            typeof(Queryable).GetMethods().GroupBy(m => m.Name).ToDictionary(g => g.Key, g => g.ToArray());

        protected OeEnumerableToQuerableVisitor()
        {
        }

        private static MethodInfo GetQuerableMethodInfo(MethodInfo enumerableMethodInfo, ReadOnlyCollection<Expression> arguments)
        {
            var enumerableTypes = new Type[arguments.Count - 1];
            for (int i = 1; i < arguments.Count; i++)
                if (arguments[i].Type.IsGenericType)
                    enumerableTypes[i - 1] = arguments[i].Type.GetGenericTypeDefinition();
                else
                    enumerableTypes[i - 1] = arguments[i].Type;

            foreach (MethodInfo methodInfo in _queryableMethods[enumerableMethodInfo.Name])
            {
                ParameterInfo[] queryableParameters = methodInfo.GetParameters();
                if (queryableParameters.Length == arguments.Count)
                {
                    bool matched = true;
                    for (int i = 1; i < queryableParameters.Length && matched; i++)
                        if (queryableParameters[i].ParameterType.IsGenericType)
                        {
                            if (queryableParameters[i].ParameterType.IsSubclassOf(typeof(LambdaExpression)))
                            {
                                Type lambdaType = queryableParameters[i].ParameterType.GetGenericArguments()[0];
                                matched = lambdaType.GetGenericTypeDefinition() == enumerableTypes[i - 1];
                            }
                            else
                            {
                                Type queryableParameter = queryableParameters[i].ParameterType.GetGenericTypeDefinition();
                                if (queryableParameter == typeof(IEnumerable<>) && typeof(IQueryable).IsAssignableFrom(enumerableTypes[i - 1]))
                                    matched = true;
                                else
                                    matched = queryableParameter == enumerableTypes[i - 1];
                            }
                        }
                        else
                            matched = queryableParameters[i].ParameterType == enumerableTypes[i - 1];

                    if (matched)
                        if (methodInfo.ReturnType == enumerableMethodInfo.ReturnType ||
                            (methodInfo.ReturnType.IsGenericType && (
                            methodInfo.ReturnType.GetGenericTypeDefinition() == typeof(IQueryable<>)) ||
                            methodInfo.ReturnType.GetGenericTypeDefinition() == typeof(IOrderedQueryable<>)))
                            return methodInfo.GetGenericMethodDefinition().MakeGenericMethod(enumerableMethodInfo.GetGenericArguments());
                }
            }

            if (enumerableMethodInfo.Name == nameof(Enumerable.Min) ||
                enumerableMethodInfo.Name == nameof(Enumerable.Max))
                return enumerableMethodInfo;

            throw new InvalidOperationException("method " + enumerableMethodInfo.Name + " not found in Querable");
        }
        public static Expression Translate(Expression expression)
        {
            return new OeEnumerableToQuerableVisitor().Visit(expression);
        }
        protected override Expression VisitConstant(ConstantExpression node)
        {
            if (node.Value is OeEnumerableStub enumerableStub)
                return Expression.Constant(enumerableStub, typeof(IQueryable<>).MakeGenericType(enumerableStub.ElementType));
            return base.VisitConstant(node);
        }
        protected override Expression VisitLambda<T>(Expression<T> node)
        {
            node = (Expression<T>)base.VisitLambda(node);
            if (node.ReturnType.IsGenericType && node.ReturnType.GetGenericTypeDefinition() == typeof(ICollection<>))
            {
                Type[] arguments = node.Type.GetGenericArguments();
                arguments[arguments.Length - 1] = typeof(IEnumerable<>).MakeGenericType(node.ReturnType.GetGenericArguments());
                Type delegateType = node.Type.GetGenericTypeDefinition().MakeGenericType(arguments);
                return Expression.Lambda(delegateType, node.Body, node.Parameters);
            }
            return node;
        }
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            ReadOnlyCollection<Expression> arguments = base.Visit(node.Arguments);
            if (node.Method.DeclaringType != typeof(Enumerable))
                return Expression.Call(base.Visit(node.Object), node.Method, arguments);

            Type genericTypeDefinition = arguments[0].Type.GetGenericTypeDefinition();
            if (genericTypeDefinition == typeof(IGrouping<,>) ||
                genericTypeDefinition == typeof(ICollection<>) ||
                genericTypeDefinition == typeof(IEnumerable<>) ||
                genericTypeDefinition == typeof(IOrderedEnumerable<>))
                return Expression.Call(base.Visit(node.Object), node.Method, arguments);

            MethodInfo querableMethodInfo = GetQuerableMethodInfo(node.Method, arguments);
            return Expression.Call(querableMethodInfo, arguments);
        }
    }
}