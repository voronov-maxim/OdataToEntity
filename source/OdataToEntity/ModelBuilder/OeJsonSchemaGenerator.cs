using Microsoft.OData.Edm;
using Microsoft.OData.Edm.Vocabularies;
using Microsoft.OData.Edm.Vocabularies.V1;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace OdataToEntity.ModelBuilder
{
    public readonly struct OeJsonSchemaGenerator
    {
        private readonly IEdmModel _edmModel;

        public OeJsonSchemaGenerator(IEdmModel edmModel)
        {
            _edmModel = edmModel;
        }

        public void Generate(Stream utf8Json)
        {
            using (var writer = new Utf8JsonWriter(utf8Json))
            {
                writer.WriteStartObject();
                writer.WriteString("$schema", "http://json-schema.org/draft-07/schema#");
                WriteDefinitions(writer);
                writer.WriteString("type", "object");
                WriteProperties(writer);
                writer.WriteEndObject();
            }
        }
        private void WriteDefinitions(Utf8JsonWriter writer)
        {
            writer.WritePropertyName("definitions");
            writer.WriteStartObject();
            WriteDefinitions(writer, _edmModel);
            foreach (IEdmModel edmModel in _edmModel.ReferencedModels)
                WriteDefinitions(writer, edmModel);
            writer.WriteEndObject();
        }
        public void WriteDefinitions(Utf8JsonWriter writer, IEdmModel edmModel)
        {
            foreach (IEdmSchemaElement schemaElement in edmModel.SchemaElements)
                if (schemaElement is IEdmEntityType entityType)
                {
                    if (!entityType.IsAbstract)
                        WriteEntity(writer, entityType);
                }
                else if (schemaElement is IEdmEnumType enumType)
                    WriteEnum(writer, enumType);
        }
        private void WriteEntity(Utf8JsonWriter writer, IEdmEntityType entityType)
        {
            writer.WritePropertyName(entityType.Name);
            writer.WriteStartObject();

            writer.WriteString("type", "object");

            writer.WritePropertyName("properties");
            writer.WriteStartObject();

            var required = new List<IEdmStructuralProperty>();
            foreach (IEdmProperty property in entityType.Properties())
            {
                if (property is IEdmStructuralProperty structuralProperty)
                {
                    WriteStructuralProperty(writer, structuralProperty);
                    if (!structuralProperty.Type.IsNullable)
                        required.Add(structuralProperty);
                }
                else if (property is IEdmNavigationProperty navigationProperty)
                    WriteNavigationProperty(writer, navigationProperty);
            }

            writer.WriteEndObject();

            if (required.Count > 0)
            {
                writer.WritePropertyName("required");
                writer.WriteStartArray();
                foreach (IEdmStructuralProperty requiredProperty in required)
                    writer.WriteStringValue(requiredProperty.Name);
                writer.WriteEndArray();
            }
            writer.WriteBoolean("additionalProperties", false);

            writer.WriteEndObject();
        }
        private static void WriteEnum(Utf8JsonWriter writer, IEdmEnumType enumType)
        {
            writer.WritePropertyName(enumType.Name);
            writer.WriteStartObject();

            writer.WriteString("type", "string");
            writer.WritePropertyName("enum");
            writer.WriteStartArray();
            foreach (IEdmEnumMember member in enumType.Members)
                writer.WriteStringValue(member.Name);
            writer.WriteEndArray();

            writer.WriteEndObject();
        }
        private static void WriteNavigationProperty(Utf8JsonWriter writer, IEdmNavigationProperty navigationProperty)
        {
            writer.WritePropertyName(navigationProperty.Name);
            writer.WriteStartObject();

            if (navigationProperty.Type.Definition is IEdmCollectionType collectionType)
            {
                var schemaElement = (IEdmSchemaElement)collectionType.ElementType.Definition;
                writer.WriteString("type", "array");

                writer.WritePropertyName("items");
                writer.WriteStartObject();
                writer.WriteString("$ref", "#/definitions/" + schemaElement.Name);
                writer.WriteEndObject();
            }
            else
            {
                var schemaElement = (IEdmSchemaElement)navigationProperty.Type.Definition;
                writer.WriteString("$ref", "#/definitions/" + schemaElement.Name);
            }

            writer.WriteEndObject();
        }
        private void WriteProperties(Utf8JsonWriter writer)
        {
            writer.WritePropertyName("properties");
            writer.WriteStartObject();

            WriteProperties(writer, _edmModel);
            foreach (IEdmModel edmModel in _edmModel.ReferencedModels)
                WriteProperties(writer, edmModel);

            writer.WriteEndObject();
        }
        private static void WriteProperties(Utf8JsonWriter writer, IEdmModel edmModel)
        {
            if (edmModel.EntityContainer != null)
                foreach (IEdmEntitySet entitySet in edmModel.EntityContainer.EntitySets())
                {
                    writer.WritePropertyName(entitySet.Name);
                    writer.WriteStartObject();

                    writer.WriteString("type", "array");

                    writer.WritePropertyName("items");
                    writer.WriteStartObject();
                    writer.WriteString("$ref", "#/definitions/" + entitySet.EntityType().Name);
                    writer.WriteEndObject();

                    writer.WriteEndObject();
                }
        }
        private void WriteStructuralProperty(Utf8JsonWriter writer, IEdmStructuralProperty structuralProperty)
        {
            if (structuralProperty.Type.Definition is IEdmEnumType enumType)
            {
                writer.WritePropertyName(structuralProperty.Name);
                writer.WriteStartObject();
                writer.WriteString("$ref", "#/definitions/" + enumType.Name);
                writer.WriteEndObject();
                return;
            }

            String typeName;
            String? format = null;
            switch (structuralProperty.Type.PrimitiveKind())
            {
                case EdmPrimitiveTypeKind.Decimal:
                case EdmPrimitiveTypeKind.Single:
                case EdmPrimitiveTypeKind.Double:
                    typeName = "number";
                    break;
                case EdmPrimitiveTypeKind.Int32:
                case EdmPrimitiveTypeKind.Int64:
                case EdmPrimitiveTypeKind.SByte:
                case EdmPrimitiveTypeKind.Byte:
                    typeName = "integer";
                    break;
                case EdmPrimitiveTypeKind.Boolean:
                    typeName = "boolean";
                    break;
                case EdmPrimitiveTypeKind.String:
                    typeName = "string";
                    break;
                case EdmPrimitiveTypeKind.DateTimeOffset:
                    typeName = "string";
                    format = "date-time";
                    break;
                case EdmPrimitiveTypeKind.Date:
                    typeName = "string";
                    format = "date-time";
                    break;
                case EdmPrimitiveTypeKind.TimeOfDay:
                    typeName = "string";
                    format = "time";
                    break;
                case EdmPrimitiveTypeKind.Guid:
                    typeName = "string";
                    format = "uuid";
                    break;
                case EdmPrimitiveTypeKind.Duration:
                    typeName = "string";
                    format = "duration";
                    break;
                case EdmPrimitiveTypeKind.None:
                    typeName = "null";
                    break;
                case EdmPrimitiveTypeKind.Binary:
                case EdmPrimitiveTypeKind.Stream:
                    writer.WritePropertyName(structuralProperty.Name);
                    writer.WriteStartObject();
                    writer.WriteString("type", "string");
                    writer.WriteString("contentEncoding", "base64");
                    writer.WriteEndObject();
                    return;
                default:
                    typeName = "object";
                    break;
            }

            writer.WritePropertyName(structuralProperty.Name);
            writer.WriteStartObject();
            writer.WriteString("type", typeName);

            if (format != null)
                writer.WriteString("format", format);

            IEdmVocabularyAnnotation? annotation = _edmModel.FindVocabularyAnnotations<IEdmVocabularyAnnotation>(structuralProperty, CoreVocabularyModel.ComputedTerm).SingleOrDefault();
            if (annotation != null && annotation.Value is IEdmBooleanConstantExpression expression && expression.Value)
                writer.WriteBoolean("readOnly", true);

            writer.WriteEndObject();
        }
    }
}
