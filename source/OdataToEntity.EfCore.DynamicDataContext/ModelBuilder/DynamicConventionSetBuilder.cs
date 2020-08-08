using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Internal;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace OdataToEntity.EfCore.DynamicDataContext.ModelBuilder
{
    public sealed class DynamicConventionSetPlugin : IConventionSetPlugin
    {
        public ConventionSet ModifyConventions(ConventionSet conventionSet)
        {
            KeyDiscoveryConvention keyDiscoveryConvention = conventionSet.EntityTypeAddedConventions.OfType<KeyDiscoveryConvention>().Single();
            foreach (PropertyInfo propertyInfo in typeof(ConventionSet).GetProperties())
                if (propertyInfo.GetValue(conventionSet) is IList list)
                    list.Remove(keyDiscoveryConvention);
            return conventionSet;
        }
    }

    public sealed class DynamicConventionSetBuilder : RuntimeConventionSetBuilder
    {
        public DynamicConventionSetBuilder(IProviderConventionSetBuilder providerConventionSetBuilder, IEnumerable<IConventionSetPlugin> plugins)
            : base(providerConventionSetBuilder, new[] { new DynamicConventionSetPlugin() })
        {
        }
    }

}
