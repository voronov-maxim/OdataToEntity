using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace OdataToEntity.Infrastructure
{
    public static class OeGenericFuncBuilder
    {
        public sealed class Canon { private Canon() { } }

        private static MethodCallExpression Create(Delegate func, Type[] typeArguments, out ParameterExpression[] parameterExpressions)
        {
            Type[] genericArguments = func.Method.GetGenericArguments();
            int typeArgumentIndex = 0;
            for (int i = 0; i < genericArguments.Length; i++)
                if (genericArguments[i] == typeof(Canon))
                    genericArguments[i] = typeArguments[typeArgumentIndex++];

            MethodInfo openMethodInfo = func.Method.GetGenericMethodDefinition();
            MethodInfo closeMethodInfo = openMethodInfo.MakeGenericMethod(genericArguments);
            ParameterInfo[] closeParameters = closeMethodInfo.GetParameters();

            ParameterInfo[] parameters = func.Method.GetParameters();
            var argumentExpressions = new Expression[parameters.Length];
            parameterExpressions = new ParameterExpression[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
                if (parameters[i].ParameterType == typeof(Canon))
                {
                    parameterExpressions[i] = Expression.Parameter(typeof(Object));
                    argumentExpressions[i] = Expression.Convert(parameterExpressions[i], closeParameters[i].ParameterType);
                }
                else
                {
                    parameterExpressions[i] = Expression.Parameter(closeParameters[i].ParameterType);
                    argumentExpressions[i] = parameterExpressions[i];
                }

            return Expression.Call(null, closeMethodInfo, argumentExpressions);
        }
        public static Func<T1, TResult> Create<T1, TResult>(Func<T1, TResult> func, params Type[] typeArguments)
        {
            MethodCallExpression call = Create(func, typeArguments, out ParameterExpression[] parameterExpressions);
            return Expression.Lambda<Func<T1, TResult>>(call, parameterExpressions).Compile();
        }
        public static Func<T1, T2, TResult> Create<T1, T2, TResult>(Func<T1, T2, TResult> func, params Type[] typeArguments)
        {
            MethodCallExpression call = Create(func, typeArguments, out ParameterExpression[] parameterExpressions);
            return Expression.Lambda<Func<T1, T2, TResult>>(call, parameterExpressions).Compile();
        }
        public static Func<T1, T2, T3, TResult> Create<T1, T2, T3, TResult>(Func<T1, T2, T3, TResult> func, params Type[] typeArguments)
        {
            MethodCallExpression call = Create(func, typeArguments, out ParameterExpression[] parameterExpressions);
            return Expression.Lambda<Func<T1, T2, T3, TResult>>(call, parameterExpressions).Compile();
        }
        public static Func<T1, T2, T3, T4, TResult> Create<T1, T2, T3, T4, TResult>(Func<T1, T2, T3, T4, TResult> func, params Type[] typeArguments)
        {
            MethodCallExpression call = Create(func, typeArguments, out ParameterExpression[] parameterExpressions);
            return Expression.Lambda<Func<T1, T2, T3, T4, TResult>>(call, parameterExpressions).Compile();
        }
    }
}
