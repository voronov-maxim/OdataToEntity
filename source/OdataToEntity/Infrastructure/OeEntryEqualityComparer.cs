using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;

namespace OdataToEntity.Infrastructure
{
    public readonly struct OeEntryEqualityComparer : IEqualityComparer<Object>, IComparer<Object>, IComparer
    {
        private sealed class TypedEqualityComparer<T> : IEqualityComparer<T>, IComparer<T> where T : notnull
        {
            private readonly OeEntryEqualityComparer _entryEqualityComparer;

            public TypedEqualityComparer(in OeEntryEqualityComparer entryEqualityComparer)
            {
                _entryEqualityComparer = entryEqualityComparer;
            }

            public int Compare([AllowNull] T x, [AllowNull] T y) => _entryEqualityComparer.Compare(x, y);
            public bool Equals([AllowNull] T x, [AllowNull] T y) => _entryEqualityComparer.Equals(x, y);
            public int GetHashCode(T obj) => _entryEqualityComparer.GetHashCode(obj);
        }

        private readonly Func<Object, Object, int>[] _propertyComparers;
        private readonly Func<Object, int>[] _propertyGetHashCodes;

        public OeEntryEqualityComparer(IReadOnlyList<Expression> propertyExpressions)
        {
            _propertyComparers = CreatePropertyComparers(propertyExpressions);
            _propertyGetHashCodes = CreatePropertyGetHashCodes(propertyExpressions);
        }

        public int Compare(Object? x, Object? y)
        {
            if (x == y)
                return 0;

            if (x == null || y == null)
                return x == null ? -1 : 1;

            Func<Object, Object, int>[] propertyComparers = _propertyComparers;
            for (int i = 0; i < propertyComparers.Length; i++)
            {
                int compare = propertyComparers[i](x, y);
                if (compare != 0)
                    return compare;
            }

            return 0;
        }
        private static Func<Object, Object, int> CreatePropertyComparer(Expression expression)
        {
            ParameterExpression xparameter = Expression.Parameter(typeof(Object));
            ParameterExpression yparameter = Expression.Parameter(typeof(Object));

            Expression xproperty, yproperty;
            if (expression is MemberExpression memberExpression)
            {
                xproperty = Parsers.OeExpressionHelper.ReplaceParameter(memberExpression, xparameter);
                yproperty = Parsers.OeExpressionHelper.ReplaceParameter(memberExpression, yparameter);
            }
            else if (expression is UnaryExpression convertExpression)
            {
                var indexExpression = (MethodCallExpression)convertExpression.Operand;
                UnaryExpression xdynamicParameter = Expression.Convert(xparameter, indexExpression.Object!.Type);
                UnaryExpression ydynamicParameter = Expression.Convert(yparameter, indexExpression.Object!.Type);

                xproperty = Expression.Convert(indexExpression.Update(xdynamicParameter, indexExpression.Arguments), expression.Type);
                yproperty = Expression.Convert(indexExpression.Update(ydynamicParameter, indexExpression.Arguments), expression.Type);
            }
            else
                throw new InvalidOperationException("Unsupported expression type");

            MethodInfo compareMethodInfo = typeof(Comparer<>).MakeGenericType(expression.Type).GetMethod(nameof(Comparer<Object>.Compare))!;
            MethodInfo defaultMethod = typeof(Comparer<>).MakeGenericType(expression.Type).GetProperty(nameof(Comparer<Object>.Default))!.GetGetMethod()!;
            MethodCallExpression compareCall = Expression.Call(Expression.Call(defaultMethod), compareMethodInfo, xproperty, yproperty);

            return (Func<Object, Object, int>)Expression.Lambda(compareCall, xparameter, yparameter).Compile();
        }
        private static Func<Object, Object, int>[] CreatePropertyComparers(IReadOnlyList<Expression> propertyExpressions)
        {
            var propertyComparers = new Func<Object, Object, int>[propertyExpressions.Count];
            for (int i = 0; i < propertyExpressions.Count; i++)
                propertyComparers[i] = CreatePropertyComparer(propertyExpressions[i]);
            return propertyComparers;
        }
        private static Func<Object, int> CreatePropertyGetHashCode(Expression expression)
        {
            ParameterExpression parameter = Expression.Parameter(typeof(Object));
            if (expression is MemberExpression memberExpression)
                expression = Parsers.OeExpressionHelper.ReplaceParameter(memberExpression, parameter);
            else if (expression is UnaryExpression convertExpression)
            {
                var indexExpression = (MethodCallExpression)convertExpression.Operand;
                indexExpression = indexExpression.Update(Expression.Convert(parameter, indexExpression.Object!.Type), indexExpression.Arguments);
                expression = Expression.Convert(indexExpression, expression.Type);
            }
            else
                throw new InvalidOperationException("Unsupported expression type");

            MethodCallExpression getHashCodeCall = Expression.Call(expression, expression.Type.GetMethod(nameof(Object.GetHashCode), Type.EmptyTypes)!);
            return (Func<Object, int>)Expression.Lambda(getHashCodeCall, parameter).Compile();
        }
        private static Func<Object, int>[] CreatePropertyGetHashCodes(IReadOnlyList<Expression> propertyExpressions)
        {
            var propertyGetHashCodes = new Func<Object, int>[propertyExpressions.Count];
            for (int i = 0; i < propertyExpressions.Count; i++)
                propertyGetHashCodes[i] = CreatePropertyGetHashCode(propertyExpressions[i]);
            return propertyGetHashCodes;
        }
        public new bool Equals(Object? x, Object? y)
        {
            return Compare(x, y) == 0;
        }
        public int GetHashCode(Object obj)
        {
            int hashCode = 0;
            Func<Object, int>[] propertyGetHashCodes = _propertyGetHashCodes;
            for (int i = 0; i < propertyGetHashCodes.Length; i++)
                hashCode = (hashCode << 5) + hashCode ^ propertyGetHashCodes[i](obj);
            return hashCode;
        }
        public IComparer<T> GetTypedComparer<T>() where T : notnull
        {
            return new TypedEqualityComparer<T>(this);
        }
        public IEqualityComparer<T> GetTypedEqualityComparer<T>() where T : notnull
        {
            return new TypedEqualityComparer<T>(this);
        }
    }
}
