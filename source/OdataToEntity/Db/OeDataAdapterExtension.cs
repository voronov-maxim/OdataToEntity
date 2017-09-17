using Microsoft.OData.Edm;
using OdataToEntity.ModelBuilder;

namespace OdataToEntity.Db
{
    public static class OeDataAdapterExtension
    {
        public static EdmModel BuildEdmModel(this OeDataAdapter dataAdapter)
        {
            var modelBuilder = new OeEdmModelBuilder(dataAdapter.EntitySetMetaAdapters.EdmModelMetadataProvider, dataAdapter.EntitySetMetaAdapters.ToDictionary());
            BuildOperations(dataAdapter, modelBuilder);
            return modelBuilder.BuildEdmModel();
        }
        public static void BuildOperations(OeDataAdapter dataAdapter, OeEdmModelBuilder modelBuilder)
        {
            OeOperationConfiguration[] operations = dataAdapter.OperationAdapter.GetOperations();
            if (operations != null)
                foreach (OeOperationConfiguration operation in operations)
                    modelBuilder.AddOperation(operation);
        }
    }
}
