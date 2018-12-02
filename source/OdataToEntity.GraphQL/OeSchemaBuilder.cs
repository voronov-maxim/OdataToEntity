using GraphQL.Types;
using Microsoft.OData.Edm;
using System.Collections.Generic;

namespace OdataToEntity.GraphQL
{
    public readonly struct OeSchemaBuilder
    {
        private readonly IEdmModel _edmModel;
        private readonly OeGraphTypeBuilder _graphTypeBuilder;

        public OeSchemaBuilder(IEdmModel edmModel)
        {
            _edmModel = edmModel;
            _graphTypeBuilder = new OeGraphTypeBuilder(edmModel);
        }

        public Schema Build()
        {
            return new Schema()
            {
                Query = CreateQuery()
            };
        }
        private static List<FieldType> CreateEntityFields(IEdmModel edmModel, OeGraphTypeBuilder graphTypeBuilder)
        {
            Db.OeDataAdapter dataAdapter = edmModel.GetDataAdapter(edmModel.EntityContainer);
            var entityFields = new List<FieldType>(dataAdapter.EntitySetAdapters.Count);
            foreach (Db.OeEntitySetAdapter entitySetAdapter in dataAdapter.EntitySetAdapters)
            {
                FieldType entityField = new FieldType()
                {
                    Name = entitySetAdapter.EntitySetName,
                    Resolver = new OeEntitySetResolver(edmModel),
                    ResolvedType = graphTypeBuilder.CreateListGraphType(entitySetAdapter.EntityType)
                };

                entityFields.Add(entityField);
            }
            return entityFields;
        }
        private ObjectGraphType CreateQuery()
        {
            var entityFields = new List<FieldType>(CreateEntityFields(_edmModel, _graphTypeBuilder));
            foreach (IEdmModel refModel in _edmModel.ReferencedModels)
                if (refModel.EntityContainer != null)
                    entityFields.AddRange(CreateEntityFields(refModel, _graphTypeBuilder));

            var query = new ObjectGraphType();
            foreach (FieldType entityField in entityFields)
            {
                _graphTypeBuilder.AddNavigationProperties(entityField);
                query.AddField(entityField);
            }
            return query;
        }
    }
}
