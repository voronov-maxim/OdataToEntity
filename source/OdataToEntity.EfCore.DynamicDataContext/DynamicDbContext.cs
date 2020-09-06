using Microsoft.EntityFrameworkCore;
using OdataToEntity.EfCore.DynamicDataContext.ModelBuilder;
using System;

namespace OdataToEntity.EfCore.DynamicDataContext
{
    public abstract class DynamicDbContext : DbContext
    {
        private readonly DynamicModelBuilder? _dynamicModelBuilder;

        protected DynamicDbContext(DbContextOptions options) : base(options)
        {
        }
        public DynamicDbContext(DbContextOptions options, in DynamicModelBuilder dynamicModelBuilder) : base(options)
        {
            _dynamicModelBuilder = dynamicModelBuilder;
        }

        protected override void OnModelCreating(Microsoft.EntityFrameworkCore.ModelBuilder modelBuilder)
        {
            if (_dynamicModelBuilder == null)
                throw new InvalidOperationException(nameof(DynamicDbContext) + " must be create via " + nameof(DynamicTypeDefinitionManager));

            _dynamicModelBuilder.Build(modelBuilder);
        }
    }
}
