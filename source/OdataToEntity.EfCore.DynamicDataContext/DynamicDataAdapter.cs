using Microsoft.OData.Edm;
using OdataToEntity.ModelBuilder;
using System;

namespace OdataToEntity.EfCore.DynamicDataContext
{
    public sealed class DynamicDataAdapter : OeEfCoreDataAdapter<DynamicDbContext>
    {
        private readonly Db.OeEntitySetAdapterCollection _dynamicEntitySetAdapters;

        public DynamicDataAdapter(DynamicTypeDefinitionManager typeDefinitionManager)
            : base(null, null, typeDefinitionManager.OperationAdapter)
        {
            TypeDefinitionManager = typeDefinitionManager;
            _dynamicEntitySetAdapters = CreateEntitySetAdapters(typeDefinitionManager);
            base.IsDatabaseNullHighestValue = typeDefinitionManager.IsDatabaseNullHighestValue;
            base.IsCaseSensitive = typeDefinitionManager.IsCaseSensitive;
        }

        public EdmModel BuildEdmModel(ModelBuilder.DynamicMetadataProvider metadataProvider)
        {
            using (DynamicDbContext context = TypeDefinitionManager.CreateDynamicDbContext())
            {
                var edmModelMetadataProvider = new ModelBuilder.DynamicEdmModelMetadataProvider(context.Model, metadataProvider, TypeDefinitionManager);
                var modelBuilder = new OeEdmModelBuilder(this, edmModelMetadataProvider);

                foreach (OeOperationConfiguration operationConfiguration in metadataProvider.GetRoutines(TypeDefinitionManager))
                    modelBuilder.AddOperation(operationConfiguration);

                return modelBuilder.BuildEdmModel();
            }
        }
        private static Db.OeEntitySetAdapterCollection CreateEntitySetAdapters(DynamicTypeDefinitionManager typeDefinitionManager)
        {
            var entitySetAdapters = new Db.OeEntitySetAdapter[typeDefinitionManager.TypeDefinitions.Count];
            int i = 0;
            foreach (DynamicTypeDefinition typeDefinition in typeDefinitionManager.TypeDefinitions)
                entitySetAdapters[i++] = CreateEntitySetAdapter(typeDefinition.DynamicTypeType, typeDefinition.TableEdmName, typeDefinition.IsQueryType);
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
