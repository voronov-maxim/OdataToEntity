using GraphQL.Resolvers;
using GraphQL.Types;
using Microsoft.OData.Edm;
using System;
using System.Collections.Generic;

namespace OdataToEntity.GraphQL
{
    public readonly struct OeSchemaBuilder
    {
        private readonly Db.OeDataAdapter _dataAdapter;
        private readonly IEdmModel _edmModel;
        private readonly OeGraphTypeBuilder _graphTypeBuilder;

        public OeSchemaBuilder(Db.OeDataAdapter dataAdapter, IEdmModel edmModel, ModelBuilder.OeEdmModelMetadataProvider modelMetadataProvider)
        {
            _dataAdapter = dataAdapter;
            _edmModel = edmModel;
            _graphTypeBuilder = new OeGraphTypeBuilder(modelMetadataProvider);
        }

        public Schema Build()
        {
            return new Schema()
            {
                Query = CreateQuery()
            };
        }
        private ObjectGraphType CreateQuery()
        {
            var entityFields = new List<FieldType>(_dataAdapter.EntitySetAdapters.Count);
            foreach (Db.OeEntitySetAdapter entitySetAdapter in _dataAdapter.EntitySetAdapters)
            {
                Type entitySetResolverType = typeof(OeEntitySetResolver<>).MakeGenericType(entitySetAdapter.EntityType);
                var entitySetResolver = (IFieldResolver)entitySetResolverType.GetConstructors()[0].Invoke(new Object[] { _dataAdapter, _edmModel});

                FieldType entityField = new FieldType()
                {
                    Name = entitySetAdapter.EntitySetName,
                    Resolver = entitySetResolver,
                    ResolvedType = _graphTypeBuilder.CreateListGraphType(entitySetAdapter.EntityType)
                };

                entityFields.Add(entityField);
            }

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
