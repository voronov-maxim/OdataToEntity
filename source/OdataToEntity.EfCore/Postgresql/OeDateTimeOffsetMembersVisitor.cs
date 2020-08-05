using OdataToEntity.Infrastructure;
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace OdataToEntity.EfCore.Postgresql
{
    public sealed class OeDateTimeOffsetMembersVisitor : ExpressionVisitor
    {
        internal sealed class Visitor : ExpressionVisitor
        {
            protected override Expression VisitMember(MemberExpression node)
            {
                Expression expression = base.Visit(node.Expression)!;
                if (expression.Type == node.Expression!.Type)
                {
                    if (node.Member is PropertyInfo property)
                    {
                        if (property.PropertyType == typeof(DateTimeOffset))
                        {
                            var propertyInfo = new OeShadowPropertyInfo(property.DeclaringType!, typeof(DateTime), property.Name);
                            return Expression.Property(expression, propertyInfo);
                        }
                        if (property.PropertyType == typeof(DateTimeOffset?))
                        {
                            var propertyInfo = new OeShadowPropertyInfo(property.DeclaringType!, typeof(DateTime?), property.Name);
                            return Expression.Property(expression, propertyInfo);
                        }
                    }
                }
                else
                    return Expression.MakeMemberAccess(expression, expression.Type.GetMember(node.Member.Name).Single());

                return node;
            }
            protected override Expression VisitUnary(UnaryExpression node)
            {
                if (node.NodeType == ExpressionType.Quote)
                    return base.Visit(node.Operand);

                if (node.NodeType == ExpressionType.Convert)
                {
                    if (node.Type == typeof(DateTimeOffset))
                        return Expression.Convert(base.Visit(node.Operand), typeof(DateTime));
                    if (node.Type == typeof(DateTimeOffset?))
                        return Expression.Convert(base.Visit(node.Operand), typeof(DateTime?));
                }

                return node;
            }
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Expression != null &&
                node.Expression.Type == typeof(DateTimeOffset) &&
                (node.Member.Name == nameof(DateTimeOffset.Day) ||
                node.Member.Name == nameof(DateTimeOffset.Month) ||
                node.Member.Name == nameof(DateTimeOffset.Year)))
                return new Visitor().Visit(node);

            return base.VisitMember(node);
        }
    }
}
