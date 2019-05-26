using Microsoft.OData.Edm;
using OdataToEntity.ModelBuilder;
using System;

namespace OdataToEntity.EfCore.DynamicDataContext
{
    public sealed class DynamicDataAdapter : OeEfCoreDataAdapter<DynamicDbContext>
    {
        private readonly Db.OeEntitySetAdapterCollection _dynamicEntitySetAdapters;
        private readonly DynamicTypeDefinitionManager _typeDefinitionManager;

        public DynamicDataAdapter(DynamicTypeDefinitionManager typeDefinitionManager)
        {
            _typeDefinitionManager = typeDefinitionManager;
            _dynamicEntitySetAdapters = CreateEntitySetAdapters(typeDefinitionManager);
        }

        private static Db.OeEntitySetAdapterCollection CreateEntitySetAdapters(DynamicTypeDefinitionManager typeDefinitionManager)
        {
            var entitySetAdapters = new Db.OeEntitySetAdapter[typeDefinitionManager.TypeDefinitions.Count];
            int i = 0;
            foreach (DynamicTypeDefinition typeDefinition in typeDefinitionManager.TypeDefinitions)
                entitySetAdapters[i++] = CreateEntitySetAdapter(typeDefinition.DynamicTypeType, typeDefinition.TableName, false);
            return new Db.OeEntitySetAdapterCollection(entitySetAdapters);
        }

        public EdmModel BuildEdmModel()
        {
            using (DynamicDbContext context = _typeDefinitionManager.CreateDynamicDbContext())
            {
                var modelBuilder = new OeEdmModelBuilder(this, new DynamicEdmModelMetadataProvider(context.Model, _typeDefinitionManager));
                return modelBuilder.BuildEdmModel();
            }
        }
        public override Object CreateDataContext()
        {
            return _typeDefinitionManager.CreateDynamicDbContext();
        }
        public override Db.OeEntitySetAdapterCollection EntitySetAdapters => _dynamicEntitySetAdapters;
    }
}
