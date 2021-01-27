using System;
using System.Linq.Expressions;
using System.Reflection;

namespace OdataToEntity.InMemory
{
    internal sealed class NullPropagationVisitor : ExpressionVisitor
    {
        public static String SafeSubstring(String text, int start, int length)
        {
            if (start >= text.Length)
                return "";

            if (start + length > text.Length)
                length = text.Length - start;
            return text.Substring(start, length);
        }
        private static int StringComapreGreater(String? strA, String? strB)
        {
            if (strA == null || strB == null)
                return -1;

            return String.Compare(strA, strB);
        }
        private static int StringComapreLess(String? strA, String? strB)
        {
            if (strA == null || strB == null)
                return 1;

            return String.Compare(strA, strB);
        }
        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Expression != null)
            {
                if (node.Expression.Type.IsClass && node.Expression is MemberExpression)
                {
                    ConstantExpression ifTrue;
                    Type propertyType = node.Type;
                    if (propertyType.IsClass || propertyType.IsInterface)
                        ifTrue = Expression.Constant(null, node.Type);
                    else
                    {
                        propertyType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
                        ifTrue = Expression.Constant(Activator.CreateInstance(propertyType), node.Type);
                    }
                    BinaryExpression test = Expression.Equal(node.Expression, Expression.Constant(null));
                    return Expression.Condition(test, ifTrue, node);
                }

                Expression expression = base.Visit(node.Expression);
                if (node.Expression != expression)
                {
                    if (node.Expression.Type != expression.Type)
                    {
                        if (Nullable.GetUnderlyingType(expression.Type) != null)
                        {
                            MethodInfo getValueOrDefault = expression.Type.GetMethod("GetValueOrDefault", Type.EmptyTypes)!;
                            expression = Expression.Call(expression, getValueOrDefault);
                            return node.Update(expression);
                        }

                        return Expression.MakeMemberAccess(expression, node.Member);
                    }

                    return node.Update(expression);
                }
            }

            return base.VisitMember(node);
        }
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Object != null && node.Method.DeclaringType == typeof(String) && node.Method.Name == nameof(String.Substring))
            {
                Func<String, int, int, String> safeSubstring = SafeSubstring;
                var arguments = new Expression[node.Arguments.Count + 1];
                arguments[0] = node.Object;
                for (int i = 1; i < arguments.Length; i++)
                    arguments[i] = node.Arguments[i - 1];
                node = Expression.Call(safeSubstring.Method, arguments);
            }
            return base.VisitMethodCall(node);
        }
        protected override Expression VisitBinary(BinaryExpression node)
        {
            if (node.NodeType == ExpressionType.LessThan || node.NodeType == ExpressionType.LessThanOrEqual)
            {
                if (node.Left is MethodCallExpression methodCall &&
                    methodCall.Object == null &&
                    methodCall.Method.DeclaringType == typeof(String) &&
                    methodCall.Method.Name == nameof(String.Compare))
                {
                    Func<String, String, int> stringComapreLess = StringComapreLess;
                    methodCall = (MethodCallExpression)base.VisitMethodCall(methodCall);
                    methodCall = Expression.Call(stringComapreLess.Method, methodCall.Arguments);
                    return node.Update(methodCall, node.Conversion, node.Right);
                }
            }
            else if (node.NodeType == ExpressionType.GreaterThan || node.NodeType == ExpressionType.GreaterThanOrEqual)
            {
                if (node.Right is MethodCallExpression methodCall &&
                    methodCall.Object == null &&
                    methodCall.Method.DeclaringType == typeof(String) &&
                    methodCall.Method.Name == nameof(String.Compare))
                {
                    Func<String, String, int> stringComapreGreater = StringComapreGreater;
                    methodCall = (MethodCallExpression)base.VisitMethodCall(methodCall);
                    methodCall = Expression.Call(stringComapreGreater.Method, methodCall.Arguments);
                    return node.Update(node.Left, node.Conversion, methodCall);
                }
            }

            return base.VisitBinary(node);
        }
        protected override Expression VisitUnary(UnaryExpression node)
        {
            if (node.NodeType == ExpressionType.Convert)
            {
                Type? operandType = Nullable.GetUnderlyingType(node.Operand.Type);
                if (operandType != null && Nullable.GetUnderlyingType(node.Type) == null)
                {
                    if (operandType == node.Type)
                        return node.Operand;

                    return Expression.Convert(node.Operand, typeof(Nullable<>).MakeGenericType(node.Type));
                }
            }
            return base.VisitUnary(node);
        }
    }
}
