using Microsoft.OData.Edm;
using OdataToEntity.ModelBuilder;
using System;

namespace OdataToEntity.EfCore.DynamicDataContext
{
    public sealed class DynamicDataAdapter : OeEfCoreDataAdapter<Types.DynamicDbContext>
    {
        private readonly Db.OeEntitySetAdapterCollection _dynamicEntitySetAdapters;

        public DynamicDataAdapter(DynamicTypeDefinitionManager typeDefinitionManager)
            : base(null, null, typeDefinitionManager.MetadataProvider.OperationAdapter)
        {
            TypeDefinitionManager = typeDefinitionManager;
            _dynamicEntitySetAdapters = CreateEntitySetAdapters(typeDefinitionManager);
            base.IsDatabaseNullHighestValue = typeDefinitionManager.MetadataProvider.IsDatabaseNullHighestValue;
        }

        public EdmModel BuildEdmModel()
        {
            using (Types.DynamicDbContext context = TypeDefinitionManager.CreateDynamicDbContext())
            {
                var modelBuilder = new OeEdmModelBuilder(this, new DynamicEdmModelMetadataProvider(context.Model, TypeDefinitionManager));
                return modelBuilder.BuildEdmModel();
            }
        }
        private static Db.OeEntitySetAdapterCollection CreateEntitySetAdapters(DynamicTypeDefinitionManager typeDefinitionManager)
        {
            var entitySetAdapters = new Db.OeEntitySetAdapter[typeDefinitionManager.TypeDefinitions.Count];
            int i = 0;
            foreach (DynamicTypeDefinition typeDefinition in typeDefinitionManager.TypeDefinitions)
                entitySetAdapters[i++] = CreateEntitySetAdapter(typeDefinition.DynamicTypeType, typeDefinition.TableName, typeDefinition.IsQueryType);
            return new Db.OeEntitySetAdapterCollection(entitySetAdapters);
        }
        public override Object CreateDataContext()
        {
            return TypeDefinitionManager.CreateDynamicDbContext();
        }

        public override Db.OeEntitySetAdapterCollection EntitySetAdapters => _dynamicEntitySetAdapters;
        public DynamicTypeDefinitionManager TypeDefinitionManager { get; }
    }
}
