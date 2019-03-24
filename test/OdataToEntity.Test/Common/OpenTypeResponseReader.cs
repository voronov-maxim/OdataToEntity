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
        public OpenTypeResponseReader(IEdmModel edmModel)
            : base(edmModel)
        {
        }

        protected override void AddItems(Object entity, PropertyInfo propertyInfo, IEnumerable values)
        {
            var openType = (SortedDictionary<String, Object>)entity;
            var list = (IList)openType[propertyInfo.Name];
            foreach (Object value in values)
                list.Add(value);
        }
        protected override Object CreateRootEntity(ODataResource resource, IReadOnlyList<NavigationProperty> navigationProperties, Type entityType)
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

            Dictionary<PropertyInfo, ODataResourceSetBase> propertyInfos = null;
            foreach (NavigationProperty property in navigationProperties)
            {
                Object value = property.Value;

                if (property.ResourceSet != null && (property.ResourceSet.Count != null || property.ResourceSet.NextPageLink != null))
                {
                    PropertyInfo clrProperty = entityType.GetProperty(property.Name);
                    if (value == null && property.ResourceSet.NextPageLink != null)
                        value = ResponseReader.CreateCollection(clrProperty.PropertyType);

                    if (value is IEnumerable collection)
                    {
                        base.NavigationProperties.Add(collection, property.ResourceSet);

                        if (propertyInfos == null)
                        {
                            propertyInfos = new Dictionary<PropertyInfo, ODataResourceSetBase>(navigationProperties.Count);
                            base.NavigationPropertyEntities.Add(openType, propertyInfos);
                        }
                        propertyInfos.Add(clrProperty, property.ResourceSet);
                    }
                }

                if (value == null)
                {
                    PropertyInfo clrProprety = entityType.GetProperty(property.Name);
                    Type type = Parsers.OeExpressionHelper.GetCollectionItemTypeOrNull(clrProprety.PropertyType);
                    if (type == null)
                        type = clrProprety.PropertyType;

                    if (Parsers.OeExpressionHelper.IsEntityType(type))
                        value = clrProprety.PropertyType;
                }

                openType.Add(property.Name, value);
            }

            return openType;
        }
        public override IEnumerable Read(Stream response)
        {
            String entitySetName = ResponseReader.GetEntitSetName(response);
            response.Position = 0;
            Db.OeEntitySetAdapter entitySetMetaAdatpter = TestHelper.FindEntitySetAdapterByName(base.EntitySetAdapters, entitySetName);
            return base.ReadImpl(response, entitySetMetaAdatpter);
        }
    }
}
