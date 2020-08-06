using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace OdataToEntity.Parsers
{
    internal static class OeFunctionBinder
    {
        public static Expression Bind(OeQueryNodeVisitor visitor, SingleValueFunctionCallNode nodeIn)
        {
            var expressions = new List<Expression>();
            foreach (QueryNode node in nodeIn.Parameters)
                expressions.Add(visitor.TranslateNode(node));

            Type? underlyingType = Nullable.GetUnderlyingType(expressions[0].Type);
            if (underlyingType != null)
                expressions[0] = Expression.Convert(expressions[0], underlyingType);

            bool isProperty = false;
            string name;
            switch (nodeIn.Name)
            {
                case "cast":
                    return CastFunction(nodeIn, expressions);
                case "ceiling":
                    return CeilingFunction(expressions);
                case "concat":
                    return ConcatFunction(expressions);
                case "contains":
                    name = "Contains";
                    break;
                case "day":
                    name = "Day";
                    isProperty = true;
                    break;
                case "endswith":
                    name = "EndsWith";
                    break;
                case "floor":
                    return FloorFunction(expressions);
                case "fractionalseconds":
                    return FractionalSecondsFunction(expressions);
                case "hour":
                    name = "Hour";
                    isProperty = true;
                    break;
                case "indexof":
                    name = "IndexOf";
                    break;
                case "length":
                    name = "Length";
                    isProperty = true;
                    break;
                case "minute":
                    name = "Minute";
                    isProperty = true;
                    break;
                case "month":
                    name = "Month";
                    isProperty = true;
                    break;
                case "round":
                    return RoundFunction(expressions);
                case "second":
                    name = "Second";
                    isProperty = true;
                    break;
                case "startswith":
                    name = "StartsWith";
                    break;
                case "substring":
                    name = "Substring";
                    break;
                case "tolower":
                    name = "ToLower";
                    break;
                case "toupper":
                    name = "ToUpper";
                    break;
                case "trim":
                    name = "Trim";
                    break;
                case "year":
                    name = "Year";
                    isProperty = true;
                    break;
                default:
                    throw new NotImplementedException(nodeIn.Name);
            }

            MethodInfo methodInfo;
            PropertyInfo propertyInfo;
            switch (expressions.Count)
            {
                case 1:
                    if (isProperty)
                    {
                        propertyInfo = expressions[0].Type.GetProperty(name)!;
                        return Expression.Property(expressions[0], propertyInfo);
                    }
                    else
                    {
                        methodInfo = expressions[0].Type.GetMethod(name, Type.EmptyTypes)!;
                        return Expression.Call(expressions[0], methodInfo);
                    }
                case 2:
                    methodInfo = expressions[0].Type.GetMethod(name, new Type[] { expressions[1].Type })!;
                    return Expression.Call(expressions[0], methodInfo, expressions[1]);
                case 3:
                    methodInfo = expressions[0].Type.GetMethod(name, new Type[] { expressions[1].Type, expressions[2].Type })!;
                    return Expression.Call(expressions[0], methodInfo, expressions[1], expressions[2]);
                default:
                    throw new NotImplementedException(name);
            }
        }
        private static Expression CastFunction(SingleValueFunctionCallNode nodeIn, List<Expression> expressions)
        {
            EdmPrimitiveTypeKind primitiveKind = nodeIn.TypeReference.PrimitiveKind();
            if (primitiveKind == EdmPrimitiveTypeKind.DateTimeOffset && expressions[0].Type == typeof(DateTime))
                return expressions[0];

            Type clrType = ModelBuilder.PrimitiveTypeHelper.GetClrType(primitiveKind);
            return Expression.Convert(expressions[0], clrType);
        }
        private static Expression CeilingFunction(List<Expression> expressions)
        {
            Type type = expressions[0].Type == typeof(double) ? typeof(double) : typeof(Decimal);
            MethodInfo methodInfo = typeof(Math).GetMethod("Ceiling", new[] { type })!;
            return Expression.Call(null, methodInfo, expressions[0]);
        }
        private static Expression ConcatFunction(List<Expression> expressions)
        {
            MethodInfo methodInfo = ((Func<String, String, String>)String.Concat).GetMethodInfo();
            return Expression.Add(expressions[0], expressions[1], methodInfo);
        }
        private static Expression FloorFunction(List<Expression> expressions)
        {
            Type type = expressions[0].Type == typeof(double) ? typeof(double) : typeof(Decimal);
            MethodInfo methodInfo = typeof(Math).GetMethod("Floor", new[] { type })!;
            return Expression.Call(null, methodInfo, expressions[0]);
        }
        private static Expression FractionalSecondsFunction(List<Expression> expressions)
        {
            PropertyInfo propertyInfo = expressions[0].Type.GetProperty("Millisecond")!;
            Expression expression = Expression.Property(expressions[0], propertyInfo);
            expression = Expression.Convert(expression, typeof(Decimal));
            return Expression.Divide(expression, Expression.Constant((Decimal)1000));
        }
        private static Expression RoundFunction(List<Expression> expressions)
        {
            Type type = expressions[0].Type == typeof(double) ? typeof(double) : typeof(Decimal);
            MethodInfo methodInfo = typeof(Math).GetMethod("Round", new[] { type })!;
            return Expression.Call(null, methodInfo, expressions[0]);
        }
    }
}
