using Microsoft.OData.Edm;
using System;

namespace OdataToEntity.ModelBuilder
{
    public sealed class ManyToManyJoinDescription
    {
        public ManyToManyJoinDescription(Type joinClassType, IEdmNavigationProperty joinNavigationProperty, IEdmNavigationProperty targetNavigationProperty)
        {
            JoinClassType = joinClassType;
            JoinNavigationProperty = joinNavigationProperty;
            TargetNavigationProperty = targetNavigationProperty;
        }

        public Type JoinClassType { get; }
        public IEdmNavigationProperty JoinNavigationProperty { get; }
        public IEdmNavigationProperty TargetNavigationProperty { get; }
    }
}
