using Microsoft.OData.Edm;
using OdataToEntity.ModelBuilder;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Text;

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
        private static void BuildOperations(OeDataAdapter dataAdapter, OeEdmModelBuilder modelBuilder)
        {
            MethodInfo[] operations = dataAdapter.GetOperations();
            if (operations != null)
                foreach (MethodInfo methodInfo in operations)
                {
                    var description = (DescriptionAttribute)methodInfo.GetCustomAttribute(typeof(DescriptionAttribute));
                    String name = description == null ? methodInfo.Name : description.Description;
                    OeOperationConfiguration functionConfiguration = modelBuilder.AddFunction(null, name);
                    foreach (ParameterInfo parameterInfo in methodInfo.GetParameters())
                        functionConfiguration.AddParameter(parameterInfo.Name, parameterInfo.ParameterType);
                    functionConfiguration.ReturnType = methodInfo.ReturnType;
                }
        }
    }
}
