using Microsoft.EntityFrameworkCore;

namespace OdataToEntity.EfCore.DynamicDataContext.Types
{
    public sealed class DynamicDbContext01 : DynamicDbContext
    {
        public DynamicDbContext01(DbContextOptions options, DynamicTypeDefinitionManager typeDefinitionManager)
            : base(options, typeDefinitionManager)
        {
        }
        public DynamicDbContext01(DbContextOptions options, in DynamicModelBuilder dynamicModelBuilder)
            : base(options, dynamicModelBuilder)
        {
        }
    }

    public sealed class DynamicDbContext02 : DynamicDbContext
    {
        public DynamicDbContext02(DbContextOptions options, DynamicTypeDefinitionManager typeDefinitionManager)
            : base(options, typeDefinitionManager)
        {
        }
        public DynamicDbContext02(DbContextOptions options, in DynamicModelBuilder dynamicModelBuilder)
            : base(options, dynamicModelBuilder)
        {
        }
    }

    public sealed class DynamicDbContext03 : DynamicDbContext
    {
        public DynamicDbContext03(DbContextOptions options, DynamicTypeDefinitionManager typeDefinitionManager)
            : base(options, typeDefinitionManager)
        {
        }
        public DynamicDbContext03(DbContextOptions options, in DynamicModelBuilder dynamicModelBuilder)
            : base(options, dynamicModelBuilder)
        {
        }
    }

    public sealed class DynamicDbContext04 : DynamicDbContext
    {
        public DynamicDbContext04(DbContextOptions options, DynamicTypeDefinitionManager typeDefinitionManager)
            : base(options, typeDefinitionManager)
        {
        }
        public DynamicDbContext04(DbContextOptions options, in DynamicModelBuilder dynamicModelBuilder)
            : base(options, dynamicModelBuilder)
        {
        }
    }
}