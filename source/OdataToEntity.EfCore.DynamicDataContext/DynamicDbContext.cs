using Microsoft.EntityFrameworkCore;
using OdataToEntity.EfCore.DynamicDataContext.ModelBuilder;

namespace OdataToEntity.EfCore.DynamicDataContext
{
    public abstract class DynamicDbContext : DbContext
    {
        private readonly DynamicModelBuilder _dynamicModelBuilder;

        protected DynamicDbContext(DbContextOptions options, DynamicTypeDefinitionManager typeDefinitionManager) : base(options)
        {
            TypeDefinitionManager = typeDefinitionManager;
        }
        public DynamicDbContext(DbContextOptions options, in DynamicModelBuilder dynamicModelBuilder) : base(options)
        {
            _dynamicModelBuilder = dynamicModelBuilder;
            TypeDefinitionManager = dynamicModelBuilder.TypeDefinitionManager;
        }

        protected override void OnModelCreating(Microsoft.EntityFrameworkCore.ModelBuilder modelBuilder)
        {
            _dynamicModelBuilder.Build(modelBuilder);
        }

        public DynamicTypeDefinitionManager TypeDefinitionManager { get; }
    }
}
