using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace OdataToEntity.EfCore.DynamicDataContext
{
    public sealed class DynamicDbContext : DbContext
    {
        private readonly DynamicModelBuilder _dynamicModelBuilder;

        public DynamicDbContext(DbContextOptions options, DynamicTypeDefinitionManager typeDefinitionManager) : base(options)
        {
            DynamicTypeDefinitionManager = typeDefinitionManager;
        }
        public DynamicDbContext(DbContextOptions options, in DynamicModelBuilder dynamicModelBuilder) : base(options)
        {
            _dynamicModelBuilder = dynamicModelBuilder;
            DynamicTypeDefinitionManager = dynamicModelBuilder.TypeDefinitionManager;
        }

        public static DbContextOptions CreateOptions()
        {
            var optionsBuilder = new DbContextOptionsBuilder<DynamicDbContext>();
            optionsBuilder.ReplaceService<IEntityMaterializerSource, DynamicEntityMaterializerSource>();
            optionsBuilder = optionsBuilder.UseSqlServer(@"Server=.\sqlexpress;Initial Catalog=OdataToEntity;Trusted_Connection=Yes;", opt => opt.UseRelationalNulls(true));
            return optionsBuilder.Options;
        }
        protected override void OnModelCreating(Microsoft.EntityFrameworkCore.ModelBuilder modelBuilder)
        {
            _dynamicModelBuilder.Build(modelBuilder);
        }

        public DynamicTypeDefinitionManager DynamicTypeDefinitionManager { get; }
    }
}
