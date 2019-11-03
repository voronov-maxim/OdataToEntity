using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace OdataToEntity.Parsers
{
    public static class OeMethodInfoHelper
    {
        private readonly static MethodInfo[] _enumerableMethods = typeof(Enumerable).GetMethods();
        private readonly static MethodInfo[] _queryableMethods = typeof(Queryable).GetMethods();

        private static MethodInfo? _anyMethodInfo;
        private static MethodInfo? _countMethodInfo;
        private static MethodInfo? _defaultIfEmpty;
        private static MethodInfo? _distinctMethodInfo;
        private static MethodInfo? _groupByMethodInfo;
        private static MethodInfo? _groupByMethodInfo2;
        private static MethodInfo? _groupJoinMethodInfo;
        private static MethodInfo? _orderByMethodInfo;
        private static MethodInfo? _orderByDescendingMethodInfo;
        private static MethodInfo? _selectManyMethodInfo;
        private static MethodInfo? _selectManyMethodInfo2;
        private static MethodInfo? _selectMethodInfo;
        private static MethodInfo? _skipMethodInfo;
        private static MethodInfo? _takeMethodInfo;
        private static MethodInfo? _thenByMethodInfo;
        private static MethodInfo? _thenByDescendingMethodInfo;
        private static MethodInfo? _whereMethodInfo;

        public static MethodInfo GetAggMethodInfo(String methodName, Type returnType)
        {
            return GetAggMethodInfo(methodName, returnType, _enumerableMethods);
        }
        private static MethodInfo GetAggMethodInfo(String methodName, Type returnType, MethodInfo[] methods)
        {
            foreach (MethodInfo methodInfo in methods)
            {
                if (String.CompareOrdinal(methodInfo.Name, methodName) != 0)
                    continue;

                ParameterInfo[] parameters = methodInfo.GetParameters();
                if (parameters.Length != 2)
                    continue;

                Type[] arguments = parameters[1].ParameterType.GetGenericArguments();
                if (arguments.Length != 2)
                    continue;

                if (arguments[1] == returnType)
                    return methodInfo;
            }
            Func<IEnumerable<Object>, Func<Object, Object>, Object> aggFunc = methodName switch
            {
                nameof(Enumerable.Max) => Enumerable.Max,
                nameof(Enumerable.Min) => Enumerable.Min,
                _ => throw new InvalidOperationException("Aggregation method " + methodName + " not found"),
            };
            return aggFunc.Method.GetGenericMethodDefinition();
        }
        public static MethodInfo GetAnyMethodInfo(Type sourceType)
        {
            if (_anyMethodInfo == null)
            {
                Func<IEnumerable<Object>, Func<Object, bool>, bool> func = Enumerable.Any;
                _anyMethodInfo = func.GetMethodInfo().GetGenericMethodDefinition();
            }
            return _anyMethodInfo.MakeGenericMethod(sourceType);
        }
        public static MethodInfo GetCountMethodInfo(Type sourceType)
        {
            if (_countMethodInfo == null)
            {
                Func<IEnumerable<Object>, int> func = Enumerable.Count;
                _countMethodInfo = func.GetMethodInfo().GetGenericMethodDefinition();
            }
            return _countMethodInfo.MakeGenericMethod(sourceType);
        }
        public static MethodInfo GetDefaultIfEmptyMethodInfo(Type sourceType)
        {
            if (_defaultIfEmpty == null)
            {
                Func<IEnumerable<Object>, Object> func = Enumerable.DefaultIfEmpty;
                _defaultIfEmpty = func.GetMethodInfo().GetGenericMethodDefinition();
            }
            return _defaultIfEmpty.MakeGenericMethod(sourceType);
        }
        public static MethodInfo GetDistinctMethodInfo(Type type)
        {
            if (_distinctMethodInfo == null)
            {
                Func<IEnumerable<Object>, Object> func = Enumerable.Distinct;
                _distinctMethodInfo = func.GetMethodInfo().GetGenericMethodDefinition();
            }
            return _distinctMethodInfo.MakeGenericMethod(type);
        }
        public static MethodInfo GetGroupByMethodInfo(Type sourceType, Type keyType)
        {
            if (_groupByMethodInfo == null)
            {
                Func<IEnumerable<Object>, Func<Object, Object>, IEnumerable<IGrouping<Object, Object>>> func = Enumerable.GroupBy;
                _groupByMethodInfo = func.GetMethodInfo().GetGenericMethodDefinition();
            }
            return _groupByMethodInfo.MakeGenericMethod(sourceType, keyType);
        }
        public static MethodInfo GetGroupByMethodInfo(Type sourceType, Type keyType, Type elementSelector)
        {
            if (_groupByMethodInfo2 == null)
            {
                Func<IEnumerable<Object>, Func<Object, Object>, Func<Object, Object>, IEnumerable<IGrouping<Object, Object>>> func = Enumerable.GroupBy;
                _groupByMethodInfo2 = func.GetMethodInfo().GetGenericMethodDefinition();
            }
            return _groupByMethodInfo2.MakeGenericMethod(sourceType, keyType, elementSelector);
        }
        public static MethodInfo GetGroupJoinMethodInfo(Type outerType, Type innerType, Type keyType, Type resultType)
        {
            if (_groupJoinMethodInfo == null)
            {
                Func<IEnumerable<Object>, IEnumerable<Object>, Func<Object, Object>, Func<Object, Object>, Func<Object, IEnumerable<Object>, Object>, IEnumerable<Object>> func = Enumerable.GroupJoin;
                _groupJoinMethodInfo = func.GetMethodInfo().GetGenericMethodDefinition();
            }
            return _groupJoinMethodInfo.MakeGenericMethod(outerType, innerType, keyType, resultType);
        }
        public static MethodInfo GetOrderByMethodInfo(Type sourceType, Type resultType)
        {
            if (_orderByMethodInfo == null)
            {
                Func<IOrderedEnumerable<Object>, Func<Object, Object>, IOrderedEnumerable<Object>> func = Enumerable.OrderBy;
                _orderByMethodInfo = func.GetMethodInfo().GetGenericMethodDefinition();
            }
            return _orderByMethodInfo.MakeGenericMethod(sourceType, resultType);
        }
        public static MethodInfo GetOrderByDescendingMethodInfo(Type sourceType, Type resultType)
        {
            if (_orderByDescendingMethodInfo == null)
            {
                Func<IOrderedEnumerable<Object>, Func<Object, Object>, IOrderedEnumerable<Object>> func = Enumerable.OrderByDescending;
                _orderByDescendingMethodInfo = func.GetMethodInfo().GetGenericMethodDefinition();
            }
            return _orderByDescendingMethodInfo.MakeGenericMethod(sourceType, resultType);
        }
        public static MethodInfo GetSelectManyMethodInfo(Type sourceType, Type resultType)
        {
            if (_selectManyMethodInfo == null)
            {
                Func<IEnumerable<Object>, Func<Object, IEnumerable<Object>>, IEnumerable<Object>> func = Enumerable.SelectMany;
                _selectManyMethodInfo = func.GetMethodInfo().GetGenericMethodDefinition();
            }
            return _selectManyMethodInfo.MakeGenericMethod(sourceType, resultType);
        }
        public static MethodInfo GetSelectManyMethodInfo(Type sourceType, Type collectionType, Type resultType)
        {
            if (_selectManyMethodInfo2 == null)
            {
                Func<IEnumerable<Object>, Func<Object, IEnumerable<Object>>, Func<Object, Object, IEnumerable<Object>>, IEnumerable<Object>> func = Enumerable.SelectMany;
                _selectManyMethodInfo2 = func.GetMethodInfo().GetGenericMethodDefinition();
            }
            return _selectManyMethodInfo2.MakeGenericMethod(sourceType, collectionType, resultType);
        }
        public static MethodInfo GetSelectMethodInfo(Type sourceType, Type resultType)
        {
            if (_selectMethodInfo == null)
            {
                Func<IEnumerable<Object>, Func<Object, Object>, IEnumerable<Object>> func = Enumerable.Select;
                _selectMethodInfo = func.GetMethodInfo().GetGenericMethodDefinition();
            }
            return _selectMethodInfo.MakeGenericMethod(sourceType, resultType);
        }
        public static MethodInfo GetSkipMethodInfo(Type sourceType)
        {
            if (_skipMethodInfo == null)
            {
                Func<IEnumerable<Object>, int, IEnumerable<Object>> func = Enumerable.Skip;
                _skipMethodInfo = func.GetMethodInfo().GetGenericMethodDefinition();
            }
            return _skipMethodInfo.MakeGenericMethod(sourceType);
        }
        public static MethodInfo GetTakeMethodInfo(Type sourceType)
        {
            if (_takeMethodInfo == null)
            {
                Func<IEnumerable<Object>, int, IEnumerable<Object>> func = Enumerable.Take;
                _takeMethodInfo = func.GetMethodInfo().GetGenericMethodDefinition();
            }
            return _takeMethodInfo.MakeGenericMethod(sourceType);
        }
        public static MethodInfo GetThenByMethodInfo(Type sourceType, Type resultType)
        {
            if (_thenByMethodInfo == null)
            {
                Func<IOrderedEnumerable<Object>, Func<Object, Object>, IOrderedEnumerable<Object>> func = Enumerable.ThenBy;
                _thenByMethodInfo = func.GetMethodInfo().GetGenericMethodDefinition();
            }
            return _thenByMethodInfo.MakeGenericMethod(sourceType, resultType);
        }
        public static MethodInfo GetThenByDescendingMethodInfo(Type sourceType, Type resultType)
        {
            if (_thenByDescendingMethodInfo == null)
            {
                Func<IOrderedEnumerable<Object>, Func<Object, Object>, IOrderedEnumerable<Object>> func = Enumerable.ThenByDescending;
                _thenByDescendingMethodInfo = func.GetMethodInfo().GetGenericMethodDefinition();
            }
            return _thenByDescendingMethodInfo.MakeGenericMethod(sourceType, resultType);
        }
        public static MethodInfo GetWhereMethodInfo(Type sourceType)
        {
            if (_whereMethodInfo == null)
            {
                Func<IEnumerable<Object>, Func<Object, bool>, IEnumerable<Object>> func = Enumerable.Where;
                _whereMethodInfo = func.GetMethodInfo().GetGenericMethodDefinition();
            }
            return _whereMethodInfo.MakeGenericMethod(sourceType);
        }
        public static MethodInfo MakeGenericMethod(MethodInfo method, IReadOnlyList<Expression> arguments)
        {
            if (!method.IsGenericMethod)
                return method;

            MethodInfo genericMethod = method.GetGenericMethodDefinition();
            Type[] openGenericArguments = genericMethod.GetGenericArguments();
            var closeGenericArguments = new Type[openGenericArguments.Length];

            if (method.Name == nameof(Enumerable.Average) ||
                method.Name == nameof(Enumerable.Max) ||
                method.Name == nameof(Enumerable.Min) ||
                method.Name == nameof(Enumerable.Sum))
            {
                MethodInfo[]? methods = null;
                if (method.DeclaringType == typeof(Enumerable))
                    methods = _enumerableMethods;
                else if (method.DeclaringType == typeof(Queryable))
                    methods = _queryableMethods;

                if (methods != null)
                {
                    Type[] genericArguments = arguments[1].Type.GetGenericArguments();
                    genericMethod = GetAggMethodInfo(method.Name, genericArguments[1], methods).GetGenericMethodDefinition();
                    closeGenericArguments[0] = genericArguments[0];
                    if (closeGenericArguments.Length == 2)
                        closeGenericArguments[1] = genericArguments[1];
                    return genericMethod.MakeGenericMethod(closeGenericArguments);
                }
            }

            ParameterInfo[] parameters = genericMethod.GetParameters();
            for (int i = 0; i < parameters.Length; i++)
            {
                ParameterInfo parameter = parameters[i];
                if (!parameter.ParameterType.IsGenericType)
                    continue;

                Type[]? parameterGenericArguments = null;
                if (typeof(Delegate).IsAssignableFrom(parameter.ParameterType))
                    parameterGenericArguments = parameter.ParameterType.GetGenericArguments();
                else if (typeof(LambdaExpression).IsAssignableFrom(parameter.ParameterType))
                    parameterGenericArguments = parameter.ParameterType.GetGenericArguments().Single().GetGenericArguments();
                else if (typeof(System.Collections.IEnumerable).IsAssignableFrom(parameter.ParameterType))
                    parameterGenericArguments = parameter.ParameterType.GetGenericArguments();

                if (parameterGenericArguments != null)
                    for (int j = 0; j < parameterGenericArguments.Length; j++)
                    {
                        bool isEnumerable = false;
                        Type parameterGenericArgument = parameterGenericArguments[j];
                        int index = Array.IndexOf(openGenericArguments, parameterGenericArgument);
                        if (index == -1)
                        {
                            isEnumerable = typeof(System.Collections.IEnumerable).IsAssignableFrom(parameterGenericArgument);
                            if (isEnumerable)
                            {
                                parameterGenericArgument = parameterGenericArgument.GetGenericArguments()[0];
                                index = Array.IndexOf(openGenericArguments, parameterGenericArgument);
                                if (index == -1)
                                    continue;
                            }
                            else
                                continue;
                        }

                        Type[]? genericArguments = null;
                        Expression expression = arguments[i];
                        if (expression is UnaryExpression unary)
                            expression = unary.Operand;

                        if (expression is LambdaExpression lambda)
                            genericArguments = lambda.Type.GetGenericArguments();
                        else if (expression is MethodCallExpression methodCall)
                            genericArguments = methodCall.Method.GetGenericArguments();
                        else if (expression is MemberExpression || expression is ConstantExpression)
                            genericArguments = expression.Type.GetGenericArguments();

                        if (genericArguments != null)
                        {
                            if (isEnumerable)
                                closeGenericArguments[index] = genericArguments[j].GetGenericArguments()[0];
                            else
                                closeGenericArguments[index] = genericArguments[j];
                        }
                    }
            }

            return genericMethod.MakeGenericMethod(closeGenericArguments);
        }
    }
}
