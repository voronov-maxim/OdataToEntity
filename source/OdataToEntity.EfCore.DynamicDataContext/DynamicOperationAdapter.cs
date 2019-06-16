using OdataToEntity.ModelBuilder;
using System;
using System.Collections.Generic;

namespace OdataToEntity.EfCore.DynamicDataContext
{
    public class DynamicOperationAdapter : OeEfCoreOperationAdapter
    {
        private readonly InformationSchema.ProviderSpecificSchema _providerSpecificSchema;

        public DynamicOperationAdapter(InformationSchema.ProviderSpecificSchema providerSpecificSchema) : base(typeof(Types.DynamicDbContext))
        {
            _providerSpecificSchema = providerSpecificSchema;
        }

        protected override IReadOnlyList<OeOperationConfiguration> GetOperationsCore(Type dataContextType)
        {
            return _providerSpecificSchema.GetRoutines();
        }
    }
}
