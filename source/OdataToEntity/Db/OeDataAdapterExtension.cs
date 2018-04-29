using Microsoft.OData.Edm;
using OdataToEntity.ModelBuilder;

namespace OdataToEntity
{
    public static class OeDataAdapterExtension
    {
        public static EdmModel BuildEdmModel(this Db.OeDataAdapter dataAdapter)
        {
            var modelBuilder = new OeEdmModelBuilder(dataAdapter.EntitySetAdapters.EdmModelMetadataProvider);
            modelBuilder.AddEntitySetRange(dataAdapter.EntitySetAdapters.GetEntitySetNamesEntityTypes());
            BuildOperations(dataAdapter, modelBuilder);
            return modelBuilder.BuildEdmModel();
        }
        public static void BuildOperations(Db.OeDataAdapter dataAdapter, OeEdmModelBuilder modelBuilder)
        {
            OeOperationConfiguration[] operations = dataAdapter.OperationAdapter.GetOperations();
            if (operations != null)
                foreach (OeOperationConfiguration operation in operations)
                    modelBuilder.AddOperation(operation);
        }
    }
}
