using System;
using System.Linq.Expressions;
using System.Reflection;

namespace OdataToEntity.Parsers
{
    public sealed class SelectNullableVisitor : ExpressionVisitor
    {
        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Expression is UnaryExpression unaryExpression && unaryExpression.NodeType == ExpressionType.Convert)
            {
                Type? underlyingType = Nullable.GetUnderlyingType(unaryExpression.Operand.Type);
                if (underlyingType != null)
                {
                    MethodInfo methodInfo = unaryExpression.Operand.Type.GetMethod(nameof(Nullable<int>.GetValueOrDefault), Type.EmptyTypes)!;
                    MethodCallExpression getValueOrDefaultExpression = Expression.Call(unaryExpression.Operand, methodInfo);
                    return Expression.MakeMemberAccess(getValueOrDefaultExpression, node.Member);
                }
            }
            return node;
        }
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.IsStatic && node.Arguments.Count == 1 && node.Arguments[0].NodeType == ExpressionType.Convert)
            {
                var unaryExpression = (UnaryExpression)node.Arguments[0];
                Type? underlyingType = Nullable.GetUnderlyingType(unaryExpression.Operand.Type);
                if (underlyingType != null)
                {
                    MethodInfo methodInfo = unaryExpression.Operand.Type.GetMethod(nameof(Nullable<int>.GetValueOrDefault), Type.EmptyTypes)!;
                    MethodCallExpression getValueOrDefaultExpression = Expression.Call(unaryExpression.Operand, methodInfo);
                    return node.Update(node.Object!, new[] { getValueOrDefaultExpression });
                }
            }
            return node;
        }
    }
}
