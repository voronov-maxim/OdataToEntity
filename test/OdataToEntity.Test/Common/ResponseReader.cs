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
        public readonly struct NavigationInfo
        {
            public NavigationInfo(String name, bool isCollection, Uri nextPageLink, long? count, Object value)
            {
                Name = name;
                IsCollection = isCollection;
                NextPageLink = nextPageLink;
                Count = count;
                Value = value;
            }

            public long? Count { get; }
            public bool IsCollection { get; }
            public String Name { get; }
            public Uri NextPageLink { get; }
            public Object Value { get; }
        }

        private sealed class StackItem
        {
            private readonly ODataItem _item;
            private readonly List<NavigationInfo> _navigationProperties;
            private Object _value;

            public StackItem(ODataItem item)
            {
                _item = item;
                _navigationProperties = new List<NavigationInfo>();
            }

            public void AddEntry(Object value)
            {
                if (Item is ODataNestedResourceInfo link)
                {
                    if (link.IsCollection.GetValueOrDefault())
                    {
                        if (value is IList list)
                            foreach (Object item in list)
                                AddToList(item);
                        else
                            AddToList(value);
                    }
                    else
                        _value = value;
                }
                else if (Item is ODataResourceSet)
                    AddToList(value);
                else
                    throw new NotSupportedException(Item.GetType().ToString());
            }
            public void AddLink(ODataNestedResourceInfo link, Object value, ODataResourceSetBase resourceSet)
            {
                _navigationProperties.Add(new NavigationInfo(link.Name, resourceSet != null, resourceSet?.NextPageLink, resourceSet?.Count, value));
            }
            private void AddToList(Object value)
            {
                if (Value == null)
                    _value = Activator.CreateInstance(typeof(List<>).MakeGenericType(value.GetType()));
                (Value as IList).Add(value);
            }

            public ODataItem Item => _item;
            public Object Value => _value;
            public IReadOnlyList<NavigationInfo> NavigationProperties => _navigationProperties;
            public ODataResourceSetBase ResourceSet { get; set; }
        }

        private static readonly Dictionary<PropertyInfo, NavigationInfo> EmptyNavigationPropertyEntities = new Dictionary<PropertyInfo, NavigationInfo>();

        public ResponseReader(IEdmModel edmModel)
        {
            EdmModel = edmModel;

            NavigationProperties = new Dictionary<Object, NavigationInfo>();
            NavigationInfoEntities = new Dictionary<Object, Dictionary<PropertyInfo, NavigationInfo>>();
        }

        protected virtual void AddItems(Object entity, PropertyInfo propertyInfo, IEnumerable values)
        {
            var collection = (IList)propertyInfo.GetValue(entity);
            if (collection == null)
            {
                collection = CreateCollection(propertyInfo.PropertyType);
                propertyInfo.SetValue(entity, collection);
            }

            foreach (Object value in values)
                collection.Add(value);
        }
        protected static IList CreateCollection(Type type)
        {
            Type itemType = OeExpressionHelper.GetCollectionItemTypeOrNull(type);
            return (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(itemType));
        }
        protected Object CreateEntity(ODataResource resource, IReadOnlyList<NavigationInfo> navigationProperties)
        {
            Db.OeEntitySetAdapter entitySetAdapter = TestHelper.FindEntitySetAdapterByTypeName(EntitySetAdapters, resource.TypeName);
            Object entity = OeEdmClrHelper.CreateEntity(entitySetAdapter.EntityType, resource);
            Dictionary<PropertyInfo, NavigationInfo> propertyInfos = null;

            foreach (NavigationInfo navigationInfo in navigationProperties)
            {
                PropertyInfo clrProperty = entitySetAdapter.EntityType.GetProperty(navigationInfo.Name);
                Object value = navigationInfo.Value;

                if ((navigationInfo.Count == null && navigationInfo.NextPageLink == null))
                {
                    if (clrProperty.GetSetMethod() != null)
                        clrProperty.SetValue(entity, value);
                }
                else
                {
                    if (value == null && navigationInfo.NextPageLink != null)
                        if (navigationInfo.IsCollection)
                            value = CreateCollection(clrProperty.PropertyType);
                        else
                            value = Activator.CreateInstance(clrProperty.PropertyType);

                    clrProperty.SetValue(entity, value);
                    NavigationProperties.Add(value, navigationInfo);

                    if (propertyInfos == null)
                    {
                        propertyInfos = new Dictionary<PropertyInfo, NavigationInfo>(navigationProperties.Count);
                        NavigationInfoEntities.Add(entity, propertyInfos);
                    }
                    propertyInfos.Add(clrProperty, navigationInfo);
                }
            }

            return entity;
        }
        protected virtual Object CreateRootEntity(ODataResource resource, IReadOnlyList<NavigationInfo> navigationProperties, Type entityType)
        {
            return CreateEntity(resource, navigationProperties);
        }
        public async Task FillNextLinkProperties(OeParser parser, CancellationToken token)
        {
            using (var response = new MemoryStream())
                foreach (KeyValuePair<Object, Dictionary<PropertyInfo, NavigationInfo>> navigationPropertyEntity in NavigationInfoEntities)
                    foreach (KeyValuePair<PropertyInfo, NavigationInfo> propertyResourceSet in navigationPropertyEntity.Value)
                    {
                        Uri requestUri = propertyResourceSet.Value.NextPageLink;
                        while (requestUri != null)
                        {
                            response.SetLength(0);
                            await parser.ExecuteGetAsync(requestUri, OeRequestHeaders.JsonDefault, response, token).ConfigureAwait(false);
                            response.Position = 0;

                            var navigationPropertyReader = new ResponseReader(EdmModel);
                            AddItems(navigationPropertyEntity.Key, propertyResourceSet.Key, navigationPropertyReader.Read(response));
                            await navigationPropertyReader.FillNextLinkProperties(parser, token);

                            requestUri = navigationPropertyReader.ResourceSet.NextPageLink;
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
        public NavigationInfo GetNavigationInfo(Object navigationProperty)
        {
            return NavigationProperties[navigationProperty];
        }
        public IReadOnlyDictionary<PropertyInfo, NavigationInfo> GetNavigationProperties(Object entity)
        {
            if (NavigationInfoEntities.TryGetValue(entity, out Dictionary<PropertyInfo, NavigationInfo> resourceSets))
                return resourceSets;
            return EmptyNavigationPropertyEntities;
        }
        public virtual IEnumerable Read(Stream response)
        {
            String entitySetName = GetEntitSetName(response);
            response.Position = 0;
            Db.OeEntitySetAdapter entitySetMetaAdatpter = TestHelper.FindEntitySetAdapterByName(EntitySetAdapters, entitySetName);
            return Read(response, entitySetMetaAdatpter);
        }
        public IEnumerable<T> Read<T>(Stream response)
        {
            Db.OeEntitySetAdapter entitySetMetaAdatpter = TestHelper.FindEntitySetAdapterByClrType(EntitySetAdapters, typeof(T));
            return Read(response, entitySetMetaAdatpter).Cast<T>();
        }
        protected IEnumerable Read(Stream response, Db.OeEntitySetAdapter entitySetMetaAdatpter)
        {
            ResourceSet = null;
            NavigationProperties.Clear();
            NavigationInfoEntities.Clear();

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
        protected Dictionary<Object, NavigationInfo> NavigationProperties { get; }
        protected Dictionary<Object, Dictionary<PropertyInfo, NavigationInfo>> NavigationInfoEntities { get; }
        public ODataResourceSetBase ResourceSet { get; protected set; }
    }
}
