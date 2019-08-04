using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;
using OdataToEntity.EfCore.DynamicDataContext.ModelBuilder;
using System.Collections;

namespace OdataToEntity.EfCore.DynamicDataContext
{
    public abstract class DynamicDbContext : DbContext
    {
        private sealed class FixProviderConventionSetBuilder : IProviderConventionSetBuilder
        {
            private readonly ProviderConventionSetBuilderDependencies _dependencies;
            private readonly ProviderConventionSetBuilder _providerConventionSetBuilder;

            public FixProviderConventionSetBuilder(ProviderConventionSetBuilderDependencies dependencies)
            {
                _dependencies = dependencies;
                _providerConventionSetBuilder = new ProviderConventionSetBuilder(dependencies);
            }

            public ConventionSet CreateConventionSet()
            {
                ConventionSet conventionSet = _providerConventionSetBuilder.CreateConventionSet();
                if (typeof(DynamicDbContext).IsAssignableFrom(_dependencies.ContextType))
                {
                    Remove((IList)conventionSet.ForeignKeyAddedConventions);
                    Remove((IList)conventionSet.EntityTypeAddedConventions);
                    Remove((IList)conventionSet.EntityTypeBaseTypeChangedConventions);
                    Remove((IList)conventionSet.KeyRemovedConventions);
                    Remove((IList)conventionSet.ForeignKeyRemovedConventions);
                    Remove((IList)conventionSet.ForeignKeyUniquenessChangedConventions);
                    Remove((IList)conventionSet.ForeignKeyOwnershipChangedConventions);
                    Remove((IList)conventionSet.PropertyFieldChangedConventions);

                    conventionSet.PropertyAddedConventions.Clear();
                }

                return conventionSet;
            }
            private static void Remove(IList list)
            {
                for (int i = 0; i < list.Count; i++)
                    if (list[i].GetType() == typeof(KeyDiscoveryConvention))
                    {
                        list.RemoveAt(i);
                        return;
                    }
            }
        }

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

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            //optionsBuilder.ReplaceService<IProviderConventionSetBuilder, FixProviderConventionSetBuilder>();zzz
            base.OnConfiguring(optionsBuilder);
        }
        protected override void OnModelCreating(Microsoft.EntityFrameworkCore.ModelBuilder modelBuilder)
        {
            _dynamicModelBuilder.Build(modelBuilder);
        }
        public DynamicTypeDefinitionManager TypeDefinitionManager { get; }
    }
}
