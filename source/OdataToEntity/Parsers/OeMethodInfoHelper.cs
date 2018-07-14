using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace OdataToEntity.Parsers
{
    public static class OeMethodInfoHelper
    {
        private static MethodInfo _anyMethodInfo;
        private static MethodInfo _countMethodInfo;
        private static MethodInfo _defaultIfEmpty;
        private static MethodInfo _distinctMethodInfo;
        private static MethodInfo _groupByMethodInfo;
        private static MethodInfo _groupByMethodInfo2;
        private static MethodInfo _groupJoinMethodInfo;
        private static MethodInfo _orderByMethodInfo;
        private static MethodInfo _orderByDescendingMethodInfo;
        private static MethodInfo _selectManyMethodInfo;
        private static MethodInfo _selectManyMethodInfo2;
        private static MethodInfo _selectMethodInfo;
        private static MethodInfo _skipMethodInfo;
        private static MethodInfo _takeMethodInfo;
        private static MethodInfo _thenByMethodInfo;
        private static MethodInfo _thenByDescendingMethodInfo;
        private static MethodInfo _whereMethodInfo;

        public static MethodInfo GetAggMethodInfo(String methodName, Type returnType)
        {
            foreach (MethodInfo methodInfo in typeof(Enumerable).GetMethods())
            {
                if (methodInfo.Name != methodName)
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

            return null;
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
    }
}
