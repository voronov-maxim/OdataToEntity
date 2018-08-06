using Microsoft.OData;
using Microsoft.OData.Edm;
using OdataToEntity.ModelBuilder;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace OdataToEntity.Parsers
{
    public readonly struct OePropertyAccessor
    {
        private readonly Func<Object, Object> _accessor;

        private OePropertyAccessor(IEdmProperty edmProperty, Func<Object, Object> accessor, MemberExpression propertyExpression, bool skipToken)
        {
            EdmProperty = edmProperty;
            _accessor = accessor;
            PropertyExpression = propertyExpression;
            SkipToken = skipToken;
            TypeAnnotation = edmProperty.DeclaringType == PrimitiveTypeHelper.TupleEdmType ? new ODataTypeAnnotation(edmProperty.Type.ShortQualifiedName()) : null;
        }

        public static OePropertyAccessor CreatePropertyAccessor(IEdmProperty edmProperty, MemberExpression propertyExpression, ParameterExpression parameter, bool skipToken)
        {
            UnaryExpression instance = Expression.Convert(propertyExpression, typeof(Object));
            var func = (Func<Object, Object>)Expression.Lambda(instance, parameter).Compile();
            return new OePropertyAccessor(edmProperty, func, propertyExpression, skipToken);
        }
        public static OePropertyAccessor[] CreateFromTuple(Type tupleType, IReadOnlyList<IEdmProperty> edmProperties, int groupItemIndex)
        {
            ParameterExpression parameter = Expression.Parameter(typeof(Object));
            IReadOnlyList<MemberExpression> itemExpressions = OeExpressionHelper.GetPropertyExpressions(Expression.Convert(parameter, tupleType));

            int aliasIndex = 0;
            var accessors = new OePropertyAccessor[edmProperties.Count];
            if (groupItemIndex >= 0)
            {
                IReadOnlyList<MemberExpression> groupExpressions = OeExpressionHelper.GetPropertyExpressions(itemExpressions[groupItemIndex]);
                for (; aliasIndex < groupExpressions.Count; aliasIndex++)
                    accessors[aliasIndex] = CreatePropertyAccessor(edmProperties[aliasIndex], groupExpressions[aliasIndex], parameter, false);
            }

            for (int itemIndex = 0; itemIndex < itemExpressions.Count; itemIndex++)
                if (itemIndex != groupItemIndex)
                {
                    accessors[aliasIndex] = CreatePropertyAccessor(edmProperties[aliasIndex], itemExpressions[itemIndex], parameter, false);
                    aliasIndex++;
                }
            return accessors;
        }
        public static OePropertyAccessor[] CreateFromType(Type clrType, IEdmEntitySetBase entitySet)
        {
            ParameterExpression parameter = Expression.Parameter(typeof(Object));
            UnaryExpression instance = Expression.Convert(parameter, clrType);
            var propertyAccessors = new List<OePropertyAccessor>();
            foreach (IEdmStructuralProperty edmProperty in entitySet.EntityType().StructuralProperties())
            {
                MemberExpression expression = Expression.Property(instance, clrType.GetProperty(edmProperty.Name));
                propertyAccessors.Add(CreatePropertyAccessor(edmProperty, expression, parameter, false));
            }
            return propertyAccessors.ToArray();
        }
        public Object GetValue(Object item)
        {
            return _accessor(item);
        }

        public IEdmProperty EdmProperty { get; }
        internal MemberExpression PropertyExpression { get; }
        public bool SkipToken { get; }
        public ODataTypeAnnotation TypeAnnotation { get; }
    }
}
