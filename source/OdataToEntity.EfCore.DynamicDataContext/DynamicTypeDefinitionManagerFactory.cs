using OdataToEntity.EfCore.DynamicDataContext.ModelBuilder;
using OdataToEntity.EfCore.DynamicDataContext.Types;
using System;
using System.Globalization;
using System.Threading;

namespace OdataToEntity.EfCore.DynamicDataContext
{
    public class DynamicTypeDefinitionManagerFactory
    {
        private static int _dynamicDbContextIndex;

        public virtual DynamicTypeDefinitionManager Create(DynamicMetadataProvider metadataProvider)
        {
            return DynamicTypeDefinitionManager.Create(metadataProvider, CreateDynamicDbContextType());
        }
        protected static Type CreateDynamicDbContextType()
        {
            int dynamicDbContextIndex = Interlocked.Increment(ref _dynamicDbContextIndex);
            String fullName = typeof(DynamicDbContext1).Namespace + "." + nameof(DynamicDbContext) + dynamicDbContextIndex.ToString(CultureInfo.InvariantCulture);
            Type? dynamicDbContextType = Type.GetType(fullName);
            if (dynamicDbContextType == null)
                throw new InvalidOperationException("DynamicDbContext out range " + dynamicDbContextIndex.ToString(CultureInfo.InvariantCulture));

            return dynamicDbContextType;
        }
    }
}
