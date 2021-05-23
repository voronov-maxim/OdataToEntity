using Microsoft.OData;
using Microsoft.OData.Edm;
using OdataToEntity.Parsers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace OdataToEntity.Test
{
    public sealed class OpenTypeResponseReader : ResponseReader
    {
        public OpenTypeResponseReader(IEdmModel edmModel, IServiceProvider serviceProvider = null)
            : base(edmModel, serviceProvider)
        {
        }

        protected override void AddItems(Object entity, PropertyInfo propertyInfo, IEnumerable values)
        {
            var openType = (SortedDictionary<String, Object>)entity;
            Object propertyValue = openType[propertyInfo.Name];
            if (propertyValue is SortedDictionary<String, Object>)
            {
                if (OeExpressionHelper.GetCollectionItemTypeOrNull(propertyInfo.PropertyType) == null)
                    foreach (Object value in values)
                    {
                        if (value is not SortedDictionary<String, Object> dict)
                            dict = (SortedDictionary<String, Object>)new OpenTypeConverter(Array.Empty<EfInclude>()).ToOpenType(value);
                        openType[propertyInfo.Name] = dict;
                    }
                else
                    throw new InvalidOperationException("Unsupported type value");
            }
            else if (propertyValue is IList<SortedDictionary<String, Object>> list)
                foreach (Object value in values)
                {
                    if (value is not SortedDictionary<String, Object> dict)
                        dict = (SortedDictionary<String, Object>)new OpenTypeConverter(Array.Empty<EfInclude>()).ToOpenType(value);
                    list.Add(dict);
                }
            else
                throw new InvalidOperationException("Unsupported type value");
        }
        protected override Object CreateEntity(Type entityType, ODataResourceBase resource)
        {
            var openType = new SortedDictionary<String, Object>(StringComparer.Ordinal);

            foreach (ODataProperty property in resource.Properties)
                if (property.Value is ODataUntypedValue)
                    openType.Add(property.Name, null);
                else if (property.Value is ODataEnumValue enumValue)
                {
                    Type enumType = Type.GetType(enumValue.TypeName);
                    openType.Add(property.Name, Enum.Parse(enumType, enumValue.Value));
                }
                else
                    openType.Add(property.Name, property.Value);

            return openType;
        }
        protected override Object CreateEntity(ODataResourceBase resource, IReadOnlyList<NavigationInfo> navigationProperties)
        {
            Db.OeEntitySetAdapter entitySetAdapter = TestHelper.FindEntitySetAdapterByTypeName(EntitySetAdapters, resource.TypeName);
            Type entityType = entitySetAdapter.EntityType;
            var openType = (SortedDictionary<String, Object>)CreateEntity(entityType, resource);

            Dictionary<PropertyInfo, NavigationInfo> propertyInfos = null;
            foreach (NavigationInfo navigationInfo in navigationProperties)
            {
                Object value = navigationInfo.Value;

                if (navigationInfo.Count != null || navigationInfo.NextPageLink != null)
                {
                    PropertyInfo clrProperty = entityType.GetProperty(navigationInfo.Name);
                    if (value == null && navigationInfo.NextPageLink != null)
                    {
                        if (navigationInfo.IsCollection)
                            value = new List<SortedDictionary<String, Object>>();
                        else
                            value = new SortedDictionary<String, Object>();
                    }
                    base.NavigationProperties.Add(value, navigationInfo);

                    if (propertyInfos == null)
                    {
                        propertyInfos = new Dictionary<PropertyInfo, NavigationInfo>(navigationProperties.Count);
                        base.NavigationInfoEntities.Add(openType, propertyInfos);
                    }
                    propertyInfos.Add(clrProperty, navigationInfo);
                }

                if (value == null)
                {
                    PropertyInfo clrProprety = entityType.GetProperty(navigationInfo.Name);
                    Type type = OeExpressionHelper.GetCollectionItemTypeOrNull(clrProprety.PropertyType);
                    if (type == null)
                        type = clrProprety.PropertyType;

                    if (OeExpressionHelper.IsEntityType(type))
                        value = DBNull.Value;
                }

                openType.Add(navigationInfo.Name, value);
            }

            return openType;
        }
        protected override ResponseReader CreateNavigationPropertyReader(IServiceProvider serviceProvider)
        {
            return new OpenTypeResponseReader(base.EdmModel, serviceProvider);
        }
        public override IEnumerable Read(Stream response)
        {
            String entitySetName = ResponseReader.GetEntitSetName(response);
            response.Position = 0;
            Db.OeEntitySetAdapter entitySetMetaAdatpter = TestHelper.FindEntitySetAdapterByName(base.EntitySetAdapters, entitySetName);
            return base.Read(response, entitySetMetaAdatpter);
        }
    }
}
