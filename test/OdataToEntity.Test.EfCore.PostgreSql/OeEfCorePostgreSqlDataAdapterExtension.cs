using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.OData.Edm;
using OdataToEntity.ModelBuilder;
using System;

namespace OdataToEntity.EfCore
{
    public static class OeEfCorePostgreSqlDataAdapterExtension
    {
        public static EdmModel BuildEdmModelFromEfCorePgSqlModel(this Db.OeDataAdapter dataAdapter, String schema)
        {
            var context = (DbContext)dataAdapter.CreateDataContext();
            try
            {
                var model = (Model)context.Model;
                model.Relational().DefaultSchema = schema;

                var metadataProvider = new OeEfCoreEdmModelMetadataProvider(context.Model);
                var modelBuilder = new OeEdmModelBuilder(metadataProvider);
                modelBuilder.AddEntitySetRange(dataAdapter.EntitySetMetaAdapters.GetEntitySetNamesEntityTypes());
                OeDataAdapterExtension.BuildOperations(dataAdapter, modelBuilder);
                return modelBuilder.BuildEdmModel();
            }
            finally
            {
                dataAdapter.CloseDataContext(context);
            }
        }
    }
}
