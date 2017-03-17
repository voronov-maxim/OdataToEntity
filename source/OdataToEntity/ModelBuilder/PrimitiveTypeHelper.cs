using Microsoft.OData.Edm;
using Microsoft.Spatial;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Reflection;

namespace OdataToEntity.ModelBuilder
{
    internal static class PrimitiveTypeHelper
    {
        private readonly static Dictionary<Type, IEdmPrimitiveType> _clrTypeMappings = new Dictionary<Type, IEdmPrimitiveType>()
        {
            {typeof(String), GetPrimitiveType(EdmPrimitiveTypeKind.String)},
            {typeof(bool), GetPrimitiveType(EdmPrimitiveTypeKind.Boolean)},
            {typeof(bool?), GetPrimitiveType(EdmPrimitiveTypeKind.Boolean)},
            {typeof(byte), GetPrimitiveType(EdmPrimitiveTypeKind.Byte)},
            {typeof(byte?), GetPrimitiveType(EdmPrimitiveTypeKind.Byte)},
            {typeof(Decimal), GetPrimitiveType(EdmPrimitiveTypeKind.Decimal)},
            {typeof(Decimal?), GetPrimitiveType(EdmPrimitiveTypeKind.Decimal)},
            {typeof(double), GetPrimitiveType(EdmPrimitiveTypeKind.Double)},
            {typeof(double?), GetPrimitiveType(EdmPrimitiveTypeKind.Double)},
            {typeof(Guid), GetPrimitiveType(EdmPrimitiveTypeKind.Guid)},
            {typeof(Guid?), GetPrimitiveType(EdmPrimitiveTypeKind.Guid)},
            {typeof(short), GetPrimitiveType(EdmPrimitiveTypeKind.Int16)},
            {typeof(short?), GetPrimitiveType(EdmPrimitiveTypeKind.Int16)},
            {typeof(int), GetPrimitiveType(EdmPrimitiveTypeKind.Int32)},
            {typeof(int?), GetPrimitiveType(EdmPrimitiveTypeKind.Int32)},
            {typeof(long), GetPrimitiveType(EdmPrimitiveTypeKind.Int64)},
            {typeof(long?), GetPrimitiveType(EdmPrimitiveTypeKind.Int64)},
            {typeof(sbyte), GetPrimitiveType(EdmPrimitiveTypeKind.SByte)},
            {typeof(sbyte?), GetPrimitiveType(EdmPrimitiveTypeKind.SByte)},
            {typeof(float), GetPrimitiveType(EdmPrimitiveTypeKind.Single)},
            {typeof(float?), GetPrimitiveType(EdmPrimitiveTypeKind.Single)},
            {typeof(byte[]), GetPrimitiveType(EdmPrimitiveTypeKind.Binary)},
            {typeof(Stream), GetPrimitiveType(EdmPrimitiveTypeKind.Stream)},
            {typeof(Geography), GetPrimitiveType(EdmPrimitiveTypeKind.Geography)},
            {typeof(GeographyPoint), GetPrimitiveType(EdmPrimitiveTypeKind.GeographyPoint)},
            {typeof(GeographyLineString), GetPrimitiveType(EdmPrimitiveTypeKind.GeographyLineString)},
            {typeof(GeographyPolygon), GetPrimitiveType(EdmPrimitiveTypeKind.GeographyPolygon)},
            {typeof(GeographyCollection), GetPrimitiveType(EdmPrimitiveTypeKind.GeographyCollection)},
            {typeof(GeographyMultiLineString), GetPrimitiveType(EdmPrimitiveTypeKind.GeographyMultiLineString)},
            {typeof(GeographyMultiPoint), GetPrimitiveType(EdmPrimitiveTypeKind.GeographyMultiPoint)},
            {typeof(GeographyMultiPolygon), GetPrimitiveType(EdmPrimitiveTypeKind.GeographyMultiPolygon)},
            {typeof(Geometry), GetPrimitiveType(EdmPrimitiveTypeKind.Geometry)},
            {typeof(GeometryPoint), GetPrimitiveType(EdmPrimitiveTypeKind.GeometryPoint)},
            {typeof(GeometryLineString), GetPrimitiveType(EdmPrimitiveTypeKind.GeometryLineString)},
            {typeof(GeometryPolygon), GetPrimitiveType(EdmPrimitiveTypeKind.GeometryPolygon)},
            {typeof(GeometryCollection), GetPrimitiveType(EdmPrimitiveTypeKind.GeometryCollection)},
            {typeof(GeometryMultiLineString), GetPrimitiveType(EdmPrimitiveTypeKind.GeometryMultiLineString)},
            {typeof(GeometryMultiPoint), GetPrimitiveType(EdmPrimitiveTypeKind.GeometryMultiPoint)},
            {typeof(GeometryMultiPolygon), GetPrimitiveType(EdmPrimitiveTypeKind.GeometryMultiPolygon)},
            {typeof(DateTimeOffset), GetPrimitiveType(EdmPrimitiveTypeKind.DateTimeOffset)},
            {typeof(DateTimeOffset?), GetPrimitiveType(EdmPrimitiveTypeKind.DateTimeOffset)},
            {typeof(TimeSpan), GetPrimitiveType(EdmPrimitiveTypeKind.Duration)},
            {typeof(TimeSpan?), GetPrimitiveType(EdmPrimitiveTypeKind.Duration)},
            {typeof(Date), GetPrimitiveType(EdmPrimitiveTypeKind.Date)},
            {typeof(Date?), GetPrimitiveType(EdmPrimitiveTypeKind.Date)},
            {typeof(TimeOfDay), GetPrimitiveType(EdmPrimitiveTypeKind.TimeOfDay)},
            {typeof(TimeOfDay?), GetPrimitiveType(EdmPrimitiveTypeKind.TimeOfDay)},
            {typeof(ushort), GetPrimitiveType(EdmPrimitiveTypeKind.Int32)},
            {typeof(ushort?), GetPrimitiveType(EdmPrimitiveTypeKind.Int32)},
            {typeof(uint), GetPrimitiveType(EdmPrimitiveTypeKind.Int64)},
            {typeof(uint?), GetPrimitiveType(EdmPrimitiveTypeKind.Int64)},
            {typeof(ulong), GetPrimitiveType(EdmPrimitiveTypeKind.Int64)},
            {typeof(ulong?), GetPrimitiveType(EdmPrimitiveTypeKind.Int64)},
            {typeof(char[]), GetPrimitiveType(EdmPrimitiveTypeKind.String)},
            {typeof(char), GetPrimitiveType(EdmPrimitiveTypeKind.String)},
            {typeof(char?), GetPrimitiveType(EdmPrimitiveTypeKind.String)},
            {typeof(DateTime), GetPrimitiveType(EdmPrimitiveTypeKind.DateTimeOffset)},
            {typeof(DateTime?), GetPrimitiveType(EdmPrimitiveTypeKind.DateTimeOffset)}
        };
        private readonly static Dictionary<EdmPrimitiveTypeKind, Type> _edmTypeMappings = CreateEdmTypeMappings(_clrTypeMappings);
        public static readonly EdmComplexType TupleEdmType = new EdmComplexType("Default", "Tupe");

        public static Type GetClrType(EdmPrimitiveTypeKind primitiveKind)
        {
            return _edmTypeMappings[primitiveKind];
        }
        private static Dictionary<EdmPrimitiveTypeKind, Type> CreateEdmTypeMappings(Dictionary<Type, IEdmPrimitiveType> clrTypeMappings)
        {
            var edmTypeMappings = new Dictionary<EdmPrimitiveTypeKind, Type>();
            foreach (KeyValuePair<Type, IEdmPrimitiveType> pair in clrTypeMappings)
                if (!(pair.Key.GetTypeInfo().IsGenericType && pair.Key.GetGenericTypeDefinition() == typeof(Nullable<>)))
                {
                    if (!edmTypeMappings.ContainsKey(pair.Value.PrimitiveKind))
                        edmTypeMappings.Add(pair.Value.PrimitiveKind, pair.Key);
                }
            return edmTypeMappings;
        }
        private static IEdmPrimitiveType GetPrimitiveType(EdmPrimitiveTypeKind primitiveKind)
        {
            return EdmCoreModel.Instance.GetPrimitiveType(primitiveKind);
        }
        public static IEdmPrimitiveType GetPrimitiveType(Type clrType)
        {
            IEdmPrimitiveType edmType;
            _clrTypeMappings.TryGetValue(clrType, out edmType);
            return edmType;
        }
        public static IEdmPrimitiveTypeReference GetPrimitiveTypeRef(Type clrType, bool nullable)
        {
            IEdmPrimitiveType primitiveEdmType = PrimitiveTypeHelper.GetPrimitiveType(clrType);
            return primitiveEdmType == null ? null : EdmCoreModel.Instance.GetPrimitive(primitiveEdmType.PrimitiveKind, nullable);
        }
        public static IEdmPrimitiveTypeReference GetPrimitiveTypeRef(PropertyDescriptor clrProperty)
        {
            IEdmPrimitiveType edmPrimitiveType = GetPrimitiveType(clrProperty.PropertyType);
            if (edmPrimitiveType == null)
                return null;

            bool nullable = IsNullable(clrProperty.PropertyType) && clrProperty.Attributes[typeof(RequiredAttribute)] == null;
            return EdmCoreModel.Instance.GetPrimitive(edmPrimitiveType.PrimitiveKind, nullable);
        }
        public static bool IsNullable(Type clrType)
        {
            return clrType.GetTypeInfo().IsClass || (clrType.GetTypeInfo().IsGenericType && clrType.GetGenericTypeDefinition() == typeof(Nullable<>));
        }

    }
}
