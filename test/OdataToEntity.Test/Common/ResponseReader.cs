using Microsoft.OData;
using Microsoft.OData.Edm;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OdataToEntity.Parsers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace OdataToEntity.Test
{
    internal sealed class ResponseReader
    {
        private struct NavigationPorperty
        {
            public readonly String Name;
            public readonly ODataResourceSetBase ResourceSet;
            public readonly Object Value;

            public NavigationPorperty(String name, Object value, ODataResourceSetBase resourceSet)
            {
                Name = name;
                Value = value;
                ResourceSet = resourceSet;
            }
        }

        private sealed class StackItem
        {
            private readonly ODataItem _item;
            private readonly List<NavigationPorperty> _navigationProperties;
            private Object _value;

            public StackItem(ODataItem item)
            {
                _item = item;
                _navigationProperties = new List<NavigationPorperty>();
            }

            public void AddEntry(Object value)
            {
                var link = Item as ODataNestedResourceInfo;
                if (link != null)
                {
                    if (link.IsCollection.GetValueOrDefault())
                    {
                        var list = value as IList;
                        if (list == null)
                            AddToList((dynamic)value);
                        else
                            foreach (Object item in list)
                                AddToList((dynamic)item);
                    }
                    else
                        _value = value;
                    return;
                }

                var set = Item as ODataResourceSet;
                if (set != null)
                {
                    AddToList((dynamic)value);
                    return;
                }

                throw new NotSupportedException(Item.GetType().ToString());
            }
            public void AddLink(ODataNestedResourceInfo link, Object value, ODataResourceSetBase resourceSet)
            {
                _navigationProperties.Add(new NavigationPorperty(link.Name, value, resourceSet));
            }
            private void AddToList<T>(T value)
            {
                if (Value == null)
                    _value = new List<T>();
                (Value as List<T>).Add(value);
            }

            public ODataItem Item => _item;
            public Object Value => _value;
            public IReadOnlyList<NavigationPorperty> NavigationProperties => _navigationProperties;
            public ODataResourceSetBase ResourceSet { get; set; }
        }

        private readonly IEdmModel _edmModel;
        private readonly Db.OeEntitySetMetaAdapterCollection _entitySetMetaAdapters;
        private readonly Dictionary<IEnumerable, ODataResourceSetBase> _navigationProperties;

        public ResponseReader(IEdmModel edmModel, Db.OeEntitySetMetaAdapterCollection entitySetMetaAdapters)
        {
            _edmModel = edmModel;
            _entitySetMetaAdapters = entitySetMetaAdapters;
            _navigationProperties = new Dictionary<IEnumerable, ODataResourceSetBase>();
        }

        private Object CreateEntity(StackItem stackItem)
        {
            var entry = (ODataResource)stackItem.Item;
            if (entry == null)
                return null;

            var entitySetMetaAdapter = _entitySetMetaAdapters.FindByTypeName(entry.TypeName);
            var entity = OeEntityItem.CreateEntity(entitySetMetaAdapter.EntityType, entry);

            foreach (NavigationPorperty navigationProperty in stackItem.NavigationProperties)
            {
                PropertyInfo clrProperty = entitySetMetaAdapter.EntityType.GetProperty(navigationProperty.Name);
                Object value = navigationProperty.Value;
                if (navigationProperty.Value == null)
                    if (navigationProperty.ResourceSet != null && navigationProperty.ResourceSet.NextPageLink != null)
                    {
                        Type itemType = OeExpressionHelper.GetCollectionItemType(clrProperty.PropertyType);
                        if (itemType != null)
                            value = Activator.CreateInstance(typeof(List<>).MakeGenericType(itemType));
                    }

                clrProperty.SetValue(entity, value);
                var collection = value as IEnumerable;
                if (collection != null)
                    _navigationProperties.Add(collection, navigationProperty.ResourceSet);
            }

            return entity;
        }
        private IList CreateEntityList(IList list)
        {
            var entities = new List<Object>(list.Count);
            foreach (StackItem item in list)
                entities.Add(CreateEntity(item));
            return entities;
        }
        private Object CreateOpenTypeEntity(StackItem stackItem)
        {
            if (stackItem.Item is ODataResource)
                return CreateOpenTypeEntity((ODataResource)stackItem.Item, stackItem.NavigationProperties);
            else if (stackItem.Item is ODataResourceSet)
                return CreateEntityList((IList)stackItem.Value);

            throw new NotSupportedException(stackItem.Item.GetType().FullName);
        }
        private JObject CreateOpenTypeEntity(ODataResource resource, IReadOnlyList<NavigationPorperty> navigationProperties)
        {
            var openType = new JObject();
            foreach (ODataProperty property in resource.Properties.OrderBy(p => p.Name))
                if (property.Value is ODataUntypedValue)
                    openType.Add(property.Name, null);
                else if (property.Value is ODataEnumValue)
                    openType.Add(property.Name, new JRaw(property.Value));
                else
                    openType.Add(property.Name, new JValue(property.Value));

            foreach (NavigationPorperty navigationProperty in navigationProperties)
            {
                Object value = navigationProperty.Value;
                if (value is StackItem)
                {
                    value = CreateEntity((StackItem)value);
                    if (value == null)
                        openType.Add(navigationProperty.Name, JValue.CreateNull());
                    else
                        openType.Add(navigationProperty.Name, JObject.FromObject(value));
                }
                else
                    openType.Add(navigationProperty.Name, JArray.FromObject(value));
            }
            return openType;
        }
        private static String GetEntitSetName(Stream response)
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
            return _navigationProperties[navigationProperty];
        }
        private bool IsOpenType(StackItem stackItem)
        {
            var entry = (ODataResource)stackItem.Item;
            return _entitySetMetaAdapters.FindByTypeName(entry.TypeName) == null;
        }
        public IEnumerable ReadFeed(Stream response)
        {
            String entitySetName = GetEntitSetName(response);
            if (entitySetName == null)
                return null;

            response.Position = 0;
            Db.OeEntitySetMetaAdapter entitySetMetaAdatpter = _entitySetMetaAdapters.FindByEntitySetName(entitySetName);

            Delegate func = (Func<Stream, Db.OeEntitySetMetaAdapter, IEnumerable<Object>>)ReadFeedImpl<Object>;
            MethodInfo genericDef = func.GetMethodInfo().GetGenericMethodDefinition();
            MethodInfo genericFunc = genericDef.MakeGenericMethod(entitySetMetaAdatpter.EntityType);
            return (IEnumerable)genericFunc.Invoke(this, new Object[] { response, entitySetMetaAdatpter });
        }
        public IEnumerable<T> ReadFeed<T>(Stream response)
        {
            Db.OeEntitySetMetaAdapter entitySetMetaAdatpter = _entitySetMetaAdapters.FindByClrType(typeof(T));
            return ReadFeedImpl<T>(response, entitySetMetaAdatpter);
        }
        public IEnumerable<JObject> ReadOpenType(Stream response)
        {
            IODataResponseMessage responseMessage = new OeInMemoryMessage(response, null);
            var settings = new ODataMessageReaderSettings() { Validations = ValidationKinds.None, EnableMessageStreamDisposal = false };
            var messageReader = new ODataMessageReader(responseMessage, settings, _edmModel);

            String entitySetName = GetEntitSetName(response);
            response.Position = 0;
            IEdmEntitySet entitySet = _edmModel.EntityContainer.FindEntitySet(entitySetName);
            ODataReader reader = messageReader.CreateODataResourceSetReader(entitySet, entitySet.EntityType());

            StackItem stackItem;
            var stack = new Stack<StackItem>();
            while (reader.Read())
            {
                switch (reader.State)
                {
                    case ODataReaderState.ResourceSetStart:
                        stack.Push(new StackItem((ODataResourceSet)reader.Item));
                        break;
                    case ODataReaderState.ResourceSetEnd:
                        stackItem = stack.Pop();
                        if (stack.Count == 0)
                        {
                            if (stackItem.Value != null)
                                foreach (StackItem entry in (IList)stackItem.Value)
                                    yield return (JObject)CreateOpenTypeEntity(entry);
                        }
                        else
                        {
                            var entries = (IList)CreateOpenTypeEntity(stackItem);
                            stack.Peek().AddEntry(entries);
                        }
                        break;
                    case ODataReaderState.ResourceStart:
                        stack.Push(new StackItem((ODataResource)reader.Item));
                        break;
                    case ODataReaderState.ResourceEnd:
                        stackItem = stack.Pop();
                        if (stack.Count == 0)
                            yield return (JObject)CreateOpenTypeEntity(stackItem);
                        else
                            stack.Peek().AddEntry(stackItem);
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
        private IEnumerable<T> ReadFeedImpl<T>(Stream response, Db.OeEntitySetMetaAdapter entitySetMetaAdatpter)
        {
            ResourceSet = null;

            IODataResponseMessage responseMessage = new OeInMemoryMessage(response, null);
            var settings = new ODataMessageReaderSettings() { EnableMessageStreamDisposal = false };
            var messageReader = new ODataMessageReader(responseMessage, settings, _edmModel);

            IEdmEntitySet entitySet = _edmModel.EntityContainer.FindEntitySet(entitySetMetaAdatpter.EntitySetName);
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

                        Object entity = CreateEntity(stackItem);
                        if (stack.Count == 0)
                            yield return (T)entity;
                        else
                            stack.Peek().AddEntry(entity);
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

        public ODataResourceSetBase ResourceSet { get; private set; }
    }
}
