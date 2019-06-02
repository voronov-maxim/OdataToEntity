using Microsoft.OData.Edm;
using Microsoft.OData.Json;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace OdataToEntity.Test
{
    public sealed class EnumJsonReader : IJsonReader
    {
        private readonly struct EntityTypeFrame
        {
            public readonly IEdmEntityType EntityType;
            public readonly bool IsCollection;

            public EntityTypeFrame(IEdmEntityType entityType, bool isCollection)
            {
                EntityType = entityType;
                IsCollection = isCollection;
            }
        }

        private readonly IEdmModel _edmModel;
        private readonly JsonTextReader _jsonTextReader;
        private readonly Stack<EntityTypeFrame> _entityTypes;
        private IEdmProperty _property;
        private Object _value;

        public EnumJsonReader(IEdmModel edmModel, TextReader textReader, bool isIeee754Compatible)
        {
            _edmModel = edmModel;

            _jsonTextReader = new JsonTextReader(textReader)
            {
                FloatParseHandling = isIeee754Compatible ? FloatParseHandling.Double : FloatParseHandling.Decimal
            };
            IsIeee754Compatible = isIeee754Compatible;

            _entityTypes = new Stack<EntityTypeFrame>();
        }

        private static JsonNodeType GetJsonNodeType(JsonToken jsonToken)
        {
            switch (jsonToken)
            {
                case JsonToken.None:
                    return JsonNodeType.None;
                case JsonToken.StartObject:
                    return JsonNodeType.StartObject;
                case JsonToken.StartArray:
                    return JsonNodeType.StartArray;
                case JsonToken.StartConstructor:
                    throw new NotSupportedException(nameof(JsonToken.StartConstructor));
                case JsonToken.PropertyName:
                    return JsonNodeType.Property;
                case JsonToken.Comment:
                    throw new NotSupportedException(nameof(JsonToken.Comment));
                case JsonToken.Raw:
                    throw new NotSupportedException(nameof(JsonToken.Raw));
                case JsonToken.Integer:
                case JsonToken.Float:
                case JsonToken.String:
                case JsonToken.Boolean:
                case JsonToken.Null:
                case JsonToken.Undefined:
                    return JsonNodeType.PrimitiveValue;
                case JsonToken.EndObject:
                    return JsonNodeType.EndObject;
                case JsonToken.EndArray:
                    return JsonNodeType.EndArray;
                case JsonToken.EndConstructor:
                    throw new NotSupportedException(nameof(JsonToken.EndConstructor));
                case JsonToken.Date:
                    return JsonNodeType.PrimitiveValue;
                case JsonToken.Bytes:
                    throw new NotSupportedException(nameof(JsonToken.Bytes));
                default:
                    throw new InvalidOperationException("Unknown JsonToken " + jsonToken.ToString());
            }
        }
        public static Object GetValue(Object value, JsonToken jsonToken)
        {
            if (jsonToken == JsonToken.Date)
            {
                var dateTime = (DateTime)value;
                switch (dateTime.Kind)
                {
                    case DateTimeKind.Unspecified:
                        return new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc));
                    case DateTimeKind.Utc:
                        return new DateTimeOffset(dateTime);
                    case DateTimeKind.Local:
                        return new DateTimeOffset(dateTime.ToUniversalTime());
                    default:
                        throw new ArgumentOutOfRangeException("Unknown DateTimeKind " + dateTime.Kind.ToString());
                }
            }
            else if (jsonToken == JsonToken.Integer && value is long longValue)
                return (int)longValue;
            else
                return value;
        }
        public bool Read()
        {
            if (_jsonTextReader.TokenType == JsonToken.PropertyName)
            {
                if (_jsonTextReader.Value.Equals("@odata.context"))
                {
                    IEdmEntityType entityType = ReadOdataContextEntityType();
                    _entityTypes.Push(new EntityTypeFrame(entityType, true));
                    return entityType != null;
                }
                else if (_jsonTextReader.Depth > 1)
                {
                    _property = _entityTypes.Peek().EntityType.FindProperty((String)_jsonTextReader.Value);
                    if (_property != null)
                    {
                        if (_property.Type.IsEnum())
                        {
                            if (_jsonTextReader.Read())
                            {
                                if (_jsonTextReader.Value is long longValue)
                                    _value = EnumHelper.ToStringLiteral((IEdmEnumTypeReference)_property.Type, longValue);

                                return true;
                            }

                            return false;
                        }
                        else if (_property.Type.IsDecimal())
                        {
                            String svalue = _jsonTextReader.ReadAsString();
                            if (svalue != null)
                            {
                                _value = Decimal.Parse(svalue, CultureInfo.InvariantCulture);
                                return true;
                            }

                            return false;
                        }
                        else if (_property is IEdmNavigationProperty navigationProperty)
                            _entityTypes.Push(new EntityTypeFrame(navigationProperty.ToEntityType(), navigationProperty.Type.IsCollection()));
                    }
                }
            }
            else if (_jsonTextReader.TokenType == JsonToken.EndObject && _jsonTextReader.Depth > 1)
            {
                _property = null;
                if (!_entityTypes.Peek().IsCollection)
                    _entityTypes.Pop();
            }
            else if (_jsonTextReader.TokenType == JsonToken.EndArray)
            {
                _property = null;
                _entityTypes.Pop();
            }

            return _jsonTextReader.Read();
        }
        private IEdmEntityType ReadOdataContextEntityType()
        {
            if (_jsonTextReader.Read())
            {
                var contextUri = new Uri((String)_jsonTextReader.Value, UriKind.Absolute);
                if (contextUri.Fragment[0] == '#')
                {
                    int i = contextUri.Fragment.IndexOf('(');
                    String entitySetName = i == -1 ? contextUri.Fragment.Substring(1) : contextUri.Fragment.Substring(1, i - 1);
                    return OeEdmClrHelper.GetEntitySet(_edmModel, entitySetName).EntityType();
                }
            }

            return null;
        }

        public Object Value
        {
            get
            {
                if (_value == null)
                    return GetValue(_jsonTextReader.Value, _jsonTextReader.TokenType);
                else
                {
                    Object value = _value;
                    _value = null;
                    return value;
                }
            }
        }
        public JsonNodeType NodeType => GetJsonNodeType(_jsonTextReader.TokenType);
        public bool IsIeee754Compatible { get; }
    }
}
