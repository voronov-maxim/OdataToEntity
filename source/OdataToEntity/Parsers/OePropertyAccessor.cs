using Microsoft.OData;
using Microsoft.OData.Edm;
using OdataToEntity.ModelBuilder;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace OdataToEntity.Parsers
{
    public readonly struct OePropertyAccessor
    {
        private readonly struct PropertyExpressionKey : IEqualityComparer<PropertyExpressionKey>
        {
            private readonly int _hashCode;
            private readonly MemberInfo[] _propertyInfos;

            private PropertyExpressionKey(MemberInfo[] memberInfos, int hashCode)
            {
                _propertyInfos = memberInfos;
                _hashCode = hashCode;
            }

            public bool Equals(PropertyExpressionKey x, PropertyExpressionKey y)
            {
                if (x._propertyInfos.Length != y._propertyInfos.Length)
                    return false;

                for (int i = 0; i < x._propertyInfos.Length; i++)
                    if (x._propertyInfos[i] != y._propertyInfos[i])
                        return false;

                return true;
            }
            public int GetHashCode(PropertyExpressionKey obj)
            {
                return obj._hashCode;
            }
            public static PropertyExpressionKey CreateKey(MemberExpression propertyExpression)
            {
                var memberInfos = new List<MemberInfo> { propertyExpression.Member };
                int hashCode = propertyExpression.Member.GetHashCode();
                while (propertyExpression.Expression is MemberExpression propertyExpression1)
                {
                    propertyExpression = propertyExpression1;
                    memberInfos.Add(propertyExpression.Member);
                    hashCode = Cache.UriCompare.OeCacheComparer.CombineHashCodes(hashCode, propertyExpression.Member.GetHashCode());
                }
                return new PropertyExpressionKey(memberInfos.ToArray(), hashCode);
            }
        }

        private readonly Func<Object?, Object?> _accessor;
        private readonly static ConcurrentDictionary<PropertyExpressionKey, Func<Object?, Object?>> _lambdas =
            new ConcurrentDictionary<PropertyExpressionKey, Func<Object?, Object?>>(new PropertyExpressionKey());

        private OePropertyAccessor(IEdmProperty edmProperty, Func<Object?, Object?> accessor, Expression propertyExpression, bool skipToken)
        {
            EdmProperty = edmProperty;
            _accessor = accessor;
            PropertyExpression = propertyExpression;
            SkipToken = skipToken;

            if (edmProperty.DeclaringType == PrimitiveTypeHelper.TupleEdmType)
            {
                String typeName = propertyExpression.Type.IsEnum ? propertyExpression.Type.FullName! : edmProperty.Type.ShortQualifiedName();
                TypeAnnotation = new ODataTypeAnnotation(typeName);
            }
            else
                TypeAnnotation = null;
        }

        public static OePropertyAccessor CreatePropertyAccessor(IEdmProperty edmProperty, MemberExpression propertyExpression, ParameterExpression parameter, bool skipToken)
        {
            PropertyExpressionKey propertyExpressionKey = PropertyExpressionKey.CreateKey(propertyExpression);
            if (!_lambdas.TryGetValue(propertyExpressionKey, out Func<Object?, Object?>? lambda))
            {
                UnaryExpression instance = Expression.Convert(propertyExpression, typeof(Object));
                lambda = (Func<Object?, Object?>)Expression.Lambda(instance, parameter).Compile();
                _lambdas[propertyExpressionKey] = lambda;
            }
            return new OePropertyAccessor(edmProperty, lambda, propertyExpression, skipToken);
        }
        public static OePropertyAccessor CreatePropertyAccessor(IEdmProperty edmProperty, MethodCallExpression indexExpression, ParameterExpression parameter, bool skipToken)
        {
            EdmPrimitiveTypeKind primitiveKind = edmProperty.Type.PrimitiveKind();
            Type propertyType = PrimitiveTypeHelper.GetClrType(primitiveKind);
            if (propertyType.IsValueType && edmProperty.Type.IsNullable)
                propertyType = typeof(Nullable<>).MakeGenericType(propertyType);

            var lambda = (Func<Object?, Object?>)Expression.Lambda(indexExpression, parameter).Compile();
            UnaryExpression convertExpression = Expression.Convert(indexExpression, propertyType);
            return new OePropertyAccessor(edmProperty, lambda, convertExpression, skipToken);
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
            if (typeof(OeIndexerProperty).IsAssignableFrom(clrType))
            {
                InterfaceMapping interfaceMapping = clrType.GetInterfaceMap(typeof(OeIndexerProperty));
                foreach (IEdmStructuralProperty edmProperty in entitySet.EntityType().StructuralProperties())
                {
                    MethodCallExpression expression = Expression.Call(instance, interfaceMapping.TargetMethods[0], Expression.Constant(edmProperty.Name));
                    propertyAccessors.Add(CreatePropertyAccessor(edmProperty, expression, parameter, false));
                }
            }
            else
            {
                foreach (IEdmStructuralProperty edmProperty in entitySet.EntityType().StructuralProperties())
                {
                    PropertyInfo? propertyInfo = clrType.GetPropertyIgnoreCaseOrNull(edmProperty);
                    if (propertyInfo == null)
                    {
                        if (!(edmProperty is OeEdmStructuralShadowProperty))
                            throw new InvalidOperationException("Property " + edmProperty.Name + " not found in clr type " + clrType.Name);
                    }
                    else
                    {
                        MemberExpression expression = Expression.Property(instance, propertyInfo);
                        propertyAccessors.Add(CreatePropertyAccessor(edmProperty, expression, parameter, false));
                    }
                }
            }
            return propertyAccessors.ToArray();
        }
        public Object? GetValue(Object? item)
        {
            return _accessor(item);
        }

        public IEdmProperty EdmProperty { get; }
        internal Expression PropertyExpression { get; }
        public bool SkipToken { get; }
        public ODataTypeAnnotation? TypeAnnotation { get; }
    }
}
