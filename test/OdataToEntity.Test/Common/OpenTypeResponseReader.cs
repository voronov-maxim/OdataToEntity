using Microsoft.OData;
using Microsoft.OData.Edm;
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
            if (propertyValue is IList list)
                foreach (Object value in values)
                    list.Add(value);
            else
            {
                IEnumerator enumerator = values.GetEnumerator();
                if (enumerator.MoveNext())
                    openType[propertyInfo.Name] = enumerator.Current;
            }
        }
        protected override Object CreateRootEntity(ODataResource resource, IReadOnlyList<NavigationInfo> navigationProperties, Type entityType)
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
                            value = ResponseReader.CreateCollection(clrProperty.PropertyType);
                        else
                            value = Activator.CreateInstance(clrProperty.PropertyType);
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
                    Type type = Parsers.OeExpressionHelper.GetCollectionItemTypeOrNull(clrProprety.PropertyType);
                    if (type == null)
                        type = clrProprety.PropertyType;

                    if (Parsers.OeExpressionHelper.IsEntityType(type))
                        value = clrProprety.PropertyType;
                }

                openType.Add(navigationInfo.Name, value);
            }

            return openType;
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
