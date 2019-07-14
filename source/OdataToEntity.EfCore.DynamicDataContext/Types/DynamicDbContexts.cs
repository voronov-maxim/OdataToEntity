using Microsoft.EntityFrameworkCore;
using OdataToEntity.EfCore.DynamicDataContext.ModelBuilder;

namespace OdataToEntity.EfCore.DynamicDataContext.Types
{
    public sealed class DynamicDbContext1 : DynamicDbContext
    {
        public DynamicDbContext1(DbContextOptions options, DynamicTypeDefinitionManager typeDefinitionManager)
            : base(options, typeDefinitionManager)
        {
        }
        public DynamicDbContext1(DbContextOptions options, in DynamicModelBuilder dynamicModelBuilder)
            : base(options, dynamicModelBuilder)
        {
        }
    }

    public sealed class DynamicDbContext2 : DynamicDbContext
    {
        public DynamicDbContext2(DbContextOptions options, DynamicTypeDefinitionManager typeDefinitionManager)
            : base(options, typeDefinitionManager)
        {
        }
        public DynamicDbContext2(DbContextOptions options, in DynamicModelBuilder dynamicModelBuilder)
            : base(options, dynamicModelBuilder)
        {
        }
    }

    public sealed class DynamicDbContext3 : DynamicDbContext
    {
        public DynamicDbContext3(DbContextOptions options, DynamicTypeDefinitionManager typeDefinitionManager)
            : base(options, typeDefinitionManager)
        {
        }
        public DynamicDbContext3(DbContextOptions options, in DynamicModelBuilder dynamicModelBuilder)
            : base(options, dynamicModelBuilder)
        {
        }
    }

    public sealed class DynamicDbContext4 : DynamicDbContext
    {
        public DynamicDbContext4(DbContextOptions options, DynamicTypeDefinitionManager typeDefinitionManager)
            : base(options, typeDefinitionManager)
        {
        }
        public DynamicDbContext4(DbContextOptions options, in DynamicModelBuilder dynamicModelBuilder)
            : base(options, dynamicModelBuilder)
        {
        }
    }
}