using Microsoft.OData;
using Microsoft.OData.Edm;
using Newtonsoft.Json;
using OdataToEntity.Parsers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity.Test
{
    public class ResponseReader
    {
        protected readonly struct NavigationProperty
        {
            public NavigationProperty(String name, Object value, ODataResourceSetBase resourceSet)
            {
                Name = name;
                Value = value;
                ResourceSet = resourceSet;
            }

            public String Name { get; }
            public ODataResourceSetBase ResourceSet { get; }
            public Object Value { get; }
        }

        private sealed class StackItem
        {
            private readonly ODataItem _item;
            private readonly List<NavigationProperty> _navigationProperties;
            private Object _value;

            public StackItem(ODataItem item)
            {
                _item = item;
                _navigationProperties = new List<NavigationProperty>();
            }

            public void AddEntry(Object value)
            {
                if (Item is ODataNestedResourceInfo link)
                {
                    if (link.IsCollection.GetValueOrDefault())
                    {
                        if (value is IList list)
                            foreach (Object item in list)
                                AddToList((dynamic)item);
                        else
                            AddToList((dynamic)value);
                    }
                    else
                        _value = value;
                    return;
                }

                if (Item is ODataResourceSet)
                {
                    AddToList((dynamic)value);
                    return;
                }

                throw new NotSupportedException(Item.GetType().ToString());
            }
            public void AddLink(ODataNestedResourceInfo link, Object value, ODataResourceSetBase resourceSet)
            {
                _navigationProperties.Add(new NavigationProperty(link.Name, value, resourceSet));
            }
            private void AddToList<T>(T value)
            {
                if (Value == null)
                    _value = new List<T>();
                (Value as List<T>).Add(value);
            }

            public ODataItem Item => _item;
            public Object Value => _value;
            public IReadOnlyList<NavigationProperty> NavigationProperties => _navigationProperties;
            public ODataResourceSetBase ResourceSet { get; set; }
        }

        private static readonly Dictionary<PropertyInfo, ODataResourceSetBase> EmptyNavigationPropertyEntities = new Dictionary<PropertyInfo, ODataResourceSetBase>();

        public ResponseReader(IEdmModel edmModel)
        {
            EdmModel = edmModel;

            NavigationProperties = new Dictionary<IEnumerable, ODataResourceSetBase>();
            NavigationPropertyEntities = new Dictionary<Object, Dictionary<PropertyInfo, ODataResourceSetBase>>();
        }

        protected virtual void AddItems(Object entity, PropertyInfo propertyInfo, IEnumerable values)
        {
            var collection = propertyInfo.GetValue(entity);
            if (collection == null)
            {
                collection = CreateCollection(propertyInfo.PropertyType);
                propertyInfo.SetValue(entity, collection);
            }

            foreach (dynamic value in values)
                ((dynamic)collection).Add(value);
        }
        protected static IEnumerable CreateCollection(Type type)
        {
            Type itemType = OeExpressionHelper.GetCollectionItemTypeOrNull(type);
            return (IEnumerable)Activator.CreateInstance(typeof(List<>).MakeGenericType(itemType));
        }
        protected Object CreateEntity(ODataResource resource, IReadOnlyList<NavigationProperty> navigationProperties)
        {
            Db.OeEntitySetAdapter entitySetAdapter = TestHelper.FindEntitySetAdapterByTypeName(EntitySetAdapters, resource.TypeName);
            Object entity = OeEdmClrHelper.CreateEntity(entitySetAdapter.EntityType, resource);
            Dictionary<PropertyInfo, ODataResourceSetBase> propertyInfos = null;

            foreach (NavigationProperty navigationProperty in navigationProperties)
            {
                PropertyInfo clrProperty = entitySetAdapter.EntityType.GetProperty(navigationProperty.Name);
                Object value = navigationProperty.Value;

                if (navigationProperty.ResourceSet == null || (navigationProperty.ResourceSet.Count == null && navigationProperty.ResourceSet.NextPageLink == null))
                {
                    if (clrProperty.GetSetMethod() != null)
                        clrProperty.SetValue(entity, value);
                    continue;
                }

                if (value == null && navigationProperty.ResourceSet.NextPageLink != null)
                    value = CreateCollection(clrProperty.PropertyType);

                clrProperty.SetValue(entity, value);
                if (value is IEnumerable collection)
                {
                    NavigationProperties.Add(collection, navigationProperty.ResourceSet);

                    if (propertyInfos == null)
                    {
                        propertyInfos = new Dictionary<PropertyInfo, ODataResourceSetBase>(navigationProperties.Count);
                        NavigationPropertyEntities.Add(entity, propertyInfos);
                    }
                    propertyInfos.Add(clrProperty, navigationProperty.ResourceSet);
                }
            }

            return entity;
        }
        protected virtual Object CreateRootEntity(ODataResource resource, IReadOnlyList<NavigationProperty> navigationProperties, Type entityType)
        {
            return CreateEntity(resource, navigationProperties);
        }
        public async Task FillNextLinkProperties(OeParser parser, CancellationToken token)
        {
            using (var response = new MemoryStream())
                foreach (KeyValuePair<Object, Dictionary<PropertyInfo, ODataResourceSetBase>> navigationPropertyEntity in NavigationPropertyEntities)
                    foreach (KeyValuePair<PropertyInfo, ODataResourceSetBase> propertyResourceSet in navigationPropertyEntity.Value)
                    {
                        response.SetLength(0);
                        await parser.ExecuteGetAsync(propertyResourceSet.Value.NextPageLink, OeRequestHeaders.JsonDefault, response, token).ConfigureAwait(false);
                        response.Position = 0;

                        var navigationPropertyReader = new ResponseReader(EdmModel);
                        AddItems(navigationPropertyEntity.Key, propertyResourceSet.Key, navigationPropertyReader.Read(response));

                        if (navigationPropertyReader.ResourceSet.NextPageLink != null)
                        {
                            response.SetLength(0);
                            await parser.ExecuteGetAsync(navigationPropertyReader.ResourceSet.NextPageLink, OeRequestHeaders.JsonDefault, response, token);
                            response.Position = 0;
                            AddItems(navigationPropertyEntity.Key, propertyResourceSet.Key, navigationPropertyReader.Read(response));
                        }
                    }
        }
        protected static String GetEntitSetName(Stream response)
        {
            using (var streamReader = new StreamReader(response, Encoding.UTF8, false, 1024, true))
            using (var jsonReader = new JsonTextReader(streamReader))
            {
                while (jsonReader.Read())
                    if (jsonReader.TokenType == JsonToken.PropertyName && (String)jsonReader.Value == "@odata.context")
                    {
                        if (jsonReader.Read())
                        {
                            var contextUri = new Uri((String)jsonReader.Value, UriKind.Absolute);
                            if (contextUri.Fragment[0] == '#')
                            {
                                int i = contextUri.Fragment.IndexOf('(');
                                if (i == -1)
                                    return contextUri.Fragment.Substring(1);
                                else
                                    return contextUri.Fragment.Substring(1, i - 1);
                            }
                        }
                        return null;
                    }
            }

            return null;
        }
        public ODataResourceSetBase GetResourceSet(IEnumerable navigationProperty)
        {
            return NavigationProperties[navigationProperty];
        }
        public IReadOnlyDictionary<PropertyInfo, ODataResourceSetBase> GetResourceSets(Object entity)
        {
            if (NavigationPropertyEntities.TryGetValue(entity, out Dictionary<PropertyInfo, ODataResourceSetBase> resourceSets))
                return resourceSets;
            return EmptyNavigationPropertyEntities;
        }
        public virtual IEnumerable Read(Stream response)
        {
            String entitySetName = GetEntitSetName(response);
            response.Position = 0;
            Db.OeEntitySetAdapter entitySetMetaAdatpter = TestHelper.FindEntitySetAdapterByName(EntitySetAdapters, entitySetName);
            return ReadImpl(response, entitySetMetaAdatpter);
        }
        public IEnumerable<T> Read<T>(Stream response)
        {
            Db.OeEntitySetAdapter entitySetMetaAdatpter = TestHelper.FindEntitySetAdapterByClrType(EntitySetAdapters, typeof(T));
            return ReadImpl(response, entitySetMetaAdatpter).Cast<T>();
        }
        protected IEnumerable ReadImpl(Stream response, Db.OeEntitySetAdapter entitySetMetaAdatpter)
        {
            ResourceSet = null;
            NavigationProperties.Clear();
            NavigationPropertyEntities.Clear();

            IODataResponseMessage responseMessage = new Infrastructure.OeInMemoryMessage(response, null);
            var settings = new ODataMessageReaderSettings() { EnableMessageStreamDisposal = false, Validations = ValidationKinds.None };
            using (var messageReader = new ODataMessageReader(responseMessage, settings, EdmModel))
            {
                IEdmEntitySet entitySet = OeEdmClrHelper.GetEntitySet(EdmModel, entitySetMetaAdatpter.EntitySetName);
                ODataReader reader = messageReader.CreateODataResourceSetReader(entitySet, entitySet.EntityType());

                var stack = new Stack<StackItem>();
                while (reader.Read())
                {
                    switch (reader.State)
                    {
                        case ODataReaderState.ResourceSetStart:
                            if (stack.Count == 0)
                                ResourceSet = (ODataResourceSetBase)reader.Item;
                            else
                                stack.Peek().ResourceSet = (ODataResourceSetBase)reader.Item;
                            break;
                        case ODataReaderState.ResourceStart:
                            stack.Push(new StackItem((ODataResource)reader.Item));
                            break;
                        case ODataReaderState.ResourceEnd:
                            StackItem stackItem = stack.Pop();

                            if (reader.Item != null)
                                if (stack.Count == 0)
                                    yield return CreateRootEntity((ODataResource)stackItem.Item, stackItem.NavigationProperties, entitySetMetaAdatpter.EntityType);
                                else
                                    stack.Peek().AddEntry(CreateEntity((ODataResource)stackItem.Item, stackItem.NavigationProperties));
                            break;
                        case ODataReaderState.NestedResourceInfoStart:
                            stack.Push(new StackItem((ODataNestedResourceInfo)reader.Item));
                            break;
                        case ODataReaderState.NestedResourceInfoEnd:
                            StackItem item = stack.Pop();
                            stack.Peek().AddLink((ODataNestedResourceInfo)item.Item, item.Value, item.ResourceSet);
                            break;
                    }
                }
            }
        }

        protected IEdmModel EdmModel { get; }
        protected Db.OeEntitySetAdapterCollection EntitySetAdapters => EdmModel.GetDataAdapter(EdmModel.EntityContainer).EntitySetAdapters;
        protected Dictionary<IEnumerable, ODataResourceSetBase> NavigationProperties { get; }
        public Dictionary<Object, Dictionary<PropertyInfo, ODataResourceSetBase>> NavigationPropertyEntities { get; }
        public ODataResourceSetBase ResourceSet { get; protected set; }
    }
}
