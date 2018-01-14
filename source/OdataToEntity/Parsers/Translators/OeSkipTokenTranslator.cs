using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace OdataToEntity.Parsers
{
    public struct OeSkipTokenTranslator
    {
        private struct OrderProperty
        {
            public readonly OrderByDirection Direction;
            public MemberExpression PropertyExpression;
            public readonly SingleValuePropertyAccessNode PropertyNode;
            public readonly Object Value;

            public OrderProperty(SingleValuePropertyAccessNode propertyNode, OrderByDirection direction, Object value)
            {
                PropertyNode = propertyNode;
                Direction = direction;
                Value = value;

                PropertyExpression = null;
            }
        }

        private readonly OeSkipTokenParser _skipTokenParser;
        private readonly OeQueryNodeVisitor _visitor;

        public OeSkipTokenTranslator(OeQueryNodeVisitor visitor, OeSkipTokenParser skipTokenParser)
        {
            _skipTokenParser = skipTokenParser;
            _visitor = visitor;
        }

        public Expression Build(Expression source, String skipToken)
        {
            OrderProperty[] orderProperties = CreateOrderProperies(_visitor.EdmModel, _skipTokenParser, skipToken);
            Expression filter = CreateFilterExpression(source, _visitor, orderProperties);

            LambdaExpression lambda = Expression.Lambda(filter, _visitor.Parameter);
            MethodInfo whereMethodInfo = OeMethodInfoHelper.GetWhereMethodInfo(_visitor.Parameter.Type);
            return Expression.Call(whereMethodInfo, source, lambda);
        }
        private static BinaryExpression CreateBinaryExpression(OeQueryNodeVisitor visitor, ref OrderProperty orderProperty)
        {
            MemberExpression propertyExpression = orderProperty.PropertyExpression;
            ConstantExpression constantExpression = Expression.Constant(orderProperty.Value, propertyExpression.Type);
            visitor.AddSkipTokenConstant(constantExpression, propertyExpression.Member.Name);

            ExpressionType binaryType;
            if (orderProperty.Value == null)
                binaryType = ExpressionType.NotEqual;
            else
                binaryType = orderProperty.Direction == OrderByDirection.Ascending ? ExpressionType.GreaterThan : ExpressionType.LessThan;

            BinaryExpression compare;
            if (propertyExpression.Type == typeof(String) && binaryType != ExpressionType.NotEqual)
            {
                Func<String, String, int> compareToFunc = String.Compare;
                MethodCallExpression compareToCall = Expression.Call(null, compareToFunc.GetMethodInfo(), propertyExpression, constantExpression);
                compare = Expression.MakeBinary(binaryType, compareToCall, OeConstantToVariableVisitor.ZeroStringCompareConstantExpression);
            }
            else
                compare = Expression.MakeBinary(binaryType, propertyExpression, constantExpression);
            return compare;
        }
        private static Expression CreateFilterExpression(Expression source, OeQueryNodeVisitor visitor, OrderProperty[] orderProperties)
        {
            var tupleProperty = new OePropertyTranslator(source);

            Expression filter = null;
            for (int i = 0; i < orderProperties.Length; i++)
            {
                BinaryExpression eqFilter = null;
                for (int j = 0; j < i; j++)
                {
                    MemberExpression propertyExpression = orderProperties[j].PropertyExpression;
                    ConstantExpression constantExpression = Expression.Constant(orderProperties[j].Value, propertyExpression.Type);
                    visitor.AddSkipTokenConstant(constantExpression, propertyExpression.Member.Name);

                    BinaryExpression eq = Expression.Equal(propertyExpression, constantExpression);
                    eqFilter = eqFilter == null ? eq : Expression.AndAlso(eqFilter, eq);
                }

                orderProperties[i].PropertyExpression = (MemberExpression)visitor.TranslateNode(orderProperties[i].PropertyNode);
                if (orderProperties[i].PropertyExpression == null)
                    orderProperties[i].PropertyExpression = tupleProperty.Build(visitor.Parameter, orderProperties[i].PropertyNode.Property);
                BinaryExpression ge = CreateBinaryExpression(visitor, ref orderProperties[i]);
                if (i == 0 && orderProperties[i].PropertyNode.Property.Type.IsNullable && orderProperties[i].Value != null)
                {
                    BinaryExpression isNull = Expression.Equal(orderProperties[i].PropertyExpression, Expression.Constant(null));
                    ge = Expression.MakeBinary(ExpressionType.OrElse, ge, isNull);
                }

                eqFilter = eqFilter == null ? ge : Expression.AndAlso(eqFilter, ge);
                filter = filter == null ? eqFilter : Expression.OrElse(filter, eqFilter);
            }
            return filter;
        }
        private static OrderProperty[] CreateOrderProperies(IEdmModel edmModel, OeSkipTokenParser skipTokenParser, String skipToken)
        {
            var orderProperties = new List<OrderProperty>();
            foreach (KeyValuePair<String, Object> keyValue in skipTokenParser.ParseSkipToken(skipToken))
            {
                OrderByClause orderBy = GetOrderBy(skipTokenParser.UniqueOrderBy, keyValue.Key);
                var propertyNode = (SingleValuePropertyAccessNode)orderBy.Expression;
                orderProperties.Add(new OrderProperty(propertyNode, orderBy.Direction, keyValue.Value));
            }
            return orderProperties.ToArray();
        }
        private static OrderByClause GetOrderBy(OrderByClause orderByClause, String propertyName)
        {
            for (; String.Compare((orderByClause.Expression as SingleValuePropertyAccessNode).Property.Name, propertyName) != 0; orderByClause = orderByClause.ThenBy)
            {
            }
            return orderByClause;
        }
    }
}
