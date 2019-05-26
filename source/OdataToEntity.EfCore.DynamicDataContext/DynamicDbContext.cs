using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

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

        public static DbContextOptions CreateOptions(bool useRelationalNulls)
        {
            var optionsBuilder = new DbContextOptionsBuilder<DynamicDbContext>();
            optionsBuilder.ReplaceService<IEntityMaterializerSource, DynamicEntityMaterializerSource>();
            optionsBuilder = optionsBuilder.UseSqlServer(@"Server=.\sqlexpress;Initial Catalog=OdataToEntity;Trusted_Connection=Yes;", opt => opt.UseRelationalNulls(useRelationalNulls));
            return optionsBuilder.Options;
        }
        protected override void OnModelCreating(Microsoft.EntityFrameworkCore.ModelBuilder modelBuilder)
        {
            _dynamicModelBuilder.Build(modelBuilder);
        }

        public DynamicTypeDefinitionManager TypeDefinitionManager { get; }
    }
}
