using System;
using System.Linq.Expressions;

namespace OdataToEntity.Ef6
{
    internal sealed class OeEf6EnumerableToQuerableVisitor : Parsers.OeEnumerableToQuerableVisitor
    {
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.Name == "GetValueOrDefault")
            {
                Type underlyingType = Nullable.GetUnderlyingType(node.Object.Type);
                if (underlyingType != null)
                    return Expression.Property(node.Object, "Value");
            }

            return base.VisitMethodCall(node);
        }
    }
}
