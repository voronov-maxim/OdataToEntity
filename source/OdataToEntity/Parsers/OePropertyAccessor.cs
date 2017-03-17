using Microsoft.OData;
using Microsoft.OData.Edm;
using OdataToEntity.ModelBuilder;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace OdataToEntity.Parsers
{
    public sealed class OePropertyAccessor
    {
        private readonly Func<Object, Object> _accessor;
        private readonly IEdmProperty _edmProperty;
        private readonly String _name;
        private readonly ODataTypeAnnotation _typeAnnotation;

        private OePropertyAccessor(IEdmProperty edmProperty, Func<Object, Object> accessor)
        {
            _name = edmProperty.Name;
            _accessor = accessor;
            _edmProperty = edmProperty;

            if (edmProperty.DeclaringType == PrimitiveTypeHelper.TupleEdmType)
                _typeAnnotation = new ODataTypeAnnotation(edmProperty.Type.ShortQualifiedName());
        }

        public static OePropertyAccessor CreatePropertyAccessor(IEdmProperty edmProperty, Expression expression, ParameterExpression parameter)
        {
            UnaryExpression instance = Expression.Convert(expression, typeof(Object));
            var func = (Func<Object, Object>)Expression.Lambda(instance, parameter).Compile();
            IEdmPrimitiveType primitiveType = PrimitiveTypeHelper.GetPrimitiveType(expression.Type);
            return new OePropertyAccessor(edmProperty, func);
        }
        public static OePropertyAccessor[] CreateFromTuple(Type tupleType, IReadOnlyList<IEdmProperty> edmProperties, int groupItemIndex)
        {
            ParameterExpression tupleParameter = Expression.Parameter(tupleType);
            ParameterExpression parameter = Expression.Parameter(typeof(Object));
            IReadOnlyList<MemberExpression> itemExpressions = OeExpressionHelper.GetPropertyExpression(Expression.Convert(parameter, tupleType));

            int aliasIndex = 0;
            var accessors = new OePropertyAccessor[edmProperties.Count];
            if (groupItemIndex >= 0)
            {
                IReadOnlyList<MemberExpression> groupExpressions = OeExpressionHelper.GetPropertyExpression(itemExpressions[groupItemIndex]);
                for (; aliasIndex < groupExpressions.Count; aliasIndex++)
                    accessors[aliasIndex] = CreatePropertyAccessor(edmProperties[aliasIndex], groupExpressions[aliasIndex], parameter);
            }

            for (int itemIndex = 0; itemIndex < itemExpressions.Count; itemIndex++)
                if (itemIndex != groupItemIndex)
                {
                    accessors[aliasIndex] = CreatePropertyAccessor(edmProperties[aliasIndex], itemExpressions[itemIndex], parameter);
                    aliasIndex++;
                }
            return accessors;
        }
        public static OePropertyAccessor[] CreateFromType(Type clrType, IEdmEntitySetBase entitySet)
        {
            ParameterExpression parameter = Expression.Parameter(typeof(Object));
            Expression instance = Expression.Convert(parameter, clrType);
            var propertyAccessors = new List<OePropertyAccessor>();
            foreach (IEdmStructuralProperty edmProperty in entitySet.EntityType().StructuralProperties())
            {
                MemberExpression expression = Expression.Property(instance, clrType.GetTypeInfo().GetProperty(edmProperty.Name));
                propertyAccessors.Add(CreatePropertyAccessor(edmProperty, expression, parameter));
            }
            return propertyAccessors.ToArray();
        }
        public static OePropertyAccessor[] CreateFromExpression(Expression source, ParameterExpression parameter, IEdmEntitySetBase entitySet)
        {
            var propertyAccessors = new List<OePropertyAccessor>();
            foreach (IEdmStructuralProperty edmProperty in entitySet.EntityType().StructuralProperties())
            {
                MemberExpression expression = Expression.Property(source, source.Type.GetTypeInfo().GetProperty(edmProperty.Name));
                propertyAccessors.Add(CreatePropertyAccessor(edmProperty, expression, parameter));
            }
            return propertyAccessors.ToArray();
        }

        public Func<Object, Object> Accessor => _accessor;
        public IEdmProperty EdmProperty => _edmProperty;
        public String Name => _name;
        public ODataTypeAnnotation TypeAnnotation => _typeAnnotation;
    }
}
