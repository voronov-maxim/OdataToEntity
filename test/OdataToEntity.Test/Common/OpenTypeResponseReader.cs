using Microsoft.OData;
using Microsoft.OData.Edm;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace OdataToEntity.Test
{
    public sealed class OpenTypeResponseReader : ResponseReader
    {
        public OpenTypeResponseReader(IEdmModel edmModel, Db.OeEntitySetMetaAdapterCollection entitySetMetaAdapters)
            : base(edmModel, entitySetMetaAdapters)
        {
        }

        protected override void AddItems(Object entity, PropertyInfo propertyInfo, IEnumerable values)
        {
            var jobject = (JObject)entity;
            var jarray = jobject[propertyInfo.Name].Value<JArray>();
            foreach (Object value in values)
                jarray.Add(JObject.FromObject(value));
        }
        protected override Object CreateRootEntity(ODataResource resource, IReadOnlyList<NavigationPorperty> navigationProperties, Type entityType)
        {
            var jproperties = new List<JProperty>();
            foreach (ODataProperty property in resource.Properties)
            {
                JProperty jproperty;
                if (property.Value is ODataUntypedValue)
                    jproperty = new JProperty(property.Name, null);
                else if (property.Value is ODataEnumValue enumValue)
                {
                    Type enumType = Type.GetType(enumValue.TypeName);
                    jproperty = new JProperty(property.Name, new JValue(Enum.Parse(enumType, enumValue.Value)));
                }
                else
                    jproperty = new JProperty(property.Name, new JValue(property.Value));
                jproperties.Add(jproperty);
            }

            var openType = new JObject();
            Dictionary<PropertyInfo, ODataResourceSetBase> propertyInfos = null;
            foreach (NavigationPorperty navigationProperty in navigationProperties)
            {
                JProperty jproperty;
                Object value = navigationProperty.Value;

                if (navigationProperty.ResourceSet == null || (navigationProperty.ResourceSet.Count == null && navigationProperty.ResourceSet.NextPageLink == null))
                {
                    if (value == null)
                        jproperty = new JProperty(navigationProperty.Name, JValue.CreateNull());
                    else
                    {
                        if (value is IEnumerable)
                            jproperty = new JProperty(navigationProperty.Name, JArray.FromObject(value));
                        else
                            jproperty = new JProperty(navigationProperty.Name, JObject.FromObject(value));
                    }
                }
                else
                {
                    PropertyInfo clrProperty = entityType.GetProperty(navigationProperty.Name);
                    if (value == null && navigationProperty.ResourceSet.NextPageLink != null)
                        value = ResponseReader.CreateCollection(clrProperty.PropertyType);

                    if (value is IEnumerable collection)
                    {
                        base.NavigationProperties.Add(collection, navigationProperty.ResourceSet);

                        if (propertyInfos == null)
                        {
                            propertyInfos = new Dictionary<PropertyInfo, ODataResourceSetBase>(navigationProperties.Count);
                            base.NavigationPropertyEntities.Add(openType, propertyInfos);
                        }
                        propertyInfos.Add(clrProperty, navigationProperty.ResourceSet);
                    }

                    jproperty = new JProperty(navigationProperty.Name, value);
                }

                jproperties.Add(jproperty);
            }

            jproperties.Sort((x, y) => String.CompareOrdinal(x.Name, y.Name));
            foreach (JProperty jproperty in jproperties)
                openType.Add(jproperty);
            return openType;
        }
        public override IEnumerable Read(Stream response)
        {
            String entitySetName = ResponseReader.GetEntitSetName(response);
            response.Position = 0;
            Db.OeEntitySetMetaAdapter entitySetMetaAdatpter = base.EntitySetMetaAdapters.FindByEntitySetName(entitySetName);
            return base.ReadImpl(response, entitySetMetaAdatpter);
        }
    }
}
