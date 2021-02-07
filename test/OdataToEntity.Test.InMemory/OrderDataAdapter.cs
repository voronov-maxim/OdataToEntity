using Microsoft.OData.Edm;
using OdataToEntity.InMemory;
using OdataToEntity.Test.InMemory;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity.Test.Model
{
    public sealed class OrderDataAdapter : InMemoryDataAdapter<InMemoryOrderContext>
    {
        private IEdmModel _edmModel;

        public OrderDataAdapter() : base(new InMemoryOrderContext(), new Cache.OeQueryCache(true))
        {
            _edmModel = null!;
        }

        public static ModelBuilder.OeEdmModelMetadataProvider CreateMetadataProvider()
        {
            return new ModelBuilder.OeEdmModelMetadataProvider();
        }
        public override Task<int> SaveChangesAsync(Object dataContext, CancellationToken cancellationToken)
        {
            foreach (InMemoryEntitySetAdapter entitySetAdapter in base.EntitySetAdapters)
                if (!entitySetAdapter.IsDbQuery)
                {
                    foreach (PropertyInfo key in ModelBuilder.OeModelBuilderHelper.GetKeyProperties(entitySetAdapter.EntityType))
                    {
                        if (key.PropertyType == typeof(int))
                            foreach (Object entity in entitySetAdapter.GetSource(dataContext))
                            {
                                var id = (int)key.GetValue(entity);
                                if (id < 0)
                                    key.SetValue(entity, -id);
                            }

                    }

                    IEdmEntityType entityType = OeEdmClrHelper.GetEntitySet(_edmModel, entitySetAdapter.EntitySetName).EntityType();
                    foreach (IEdmNavigationProperty navigationProperty in entityType.NavigationProperties())
                    {
                        IEnumerable<IEdmStructuralProperty> edmProperties = navigationProperty.DependentProperties();
                        if (edmProperties != null)
                            foreach (IEdmStructuralProperty edmProperty in edmProperties)
                            {
                                if (edmProperty.DeclaringType != entityType)
                                    break;

                                PropertyInfo clrProperty = entitySetAdapter.EntityType.GetProperty(edmProperty.Name)!;
                                if (clrProperty.PropertyType == typeof(int) || clrProperty.PropertyType == typeof(int?))
                                    foreach (Object entity in entitySetAdapter.GetSource(dataContext))
                                    {
                                        var id = (int?)clrProperty.GetValue(entity);
                                        if (id < 0)
                                            clrProperty.SetValue(entity, -id);
                                    }
                            }
                    }
                }

            return base.SaveChangesAsync(dataContext, cancellationToken);
        }
        protected override void SetEdmModel(IEdmModel edmModel)
        {
            _edmModel = edmModel;
        }
    }

    public sealed class Order2DataAdapter : InMemoryDataAdapter<InMemoryOrder2Context>
    {
        public Order2DataAdapter() : base(new InMemoryOrder2Context(), new Cache.OeQueryCache(false))
        {
        }
    }
}
