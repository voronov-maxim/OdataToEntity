using Microsoft.OData;
using Microsoft.OData.Edm;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace OdataToEntity.Test
{
    internal sealed class ResponseReader
    {
        private sealed class StackItem
        {
            private readonly ODataItem _item;
            private readonly List<KeyValuePair<String, Object>> _navigationProperties;
            private Object _value;

            public StackItem(ODataItem item)
            {
                _item = item;
                _navigationProperties = new List<KeyValuePair<String, Object>>();
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
            public void AddLink(ODataNestedResourceInfo link, Object value)
            {
                _navigationProperties.Add(new KeyValuePair<String, Object>(link.Name, value));
            }
            private void AddToList<T>(T value)
            {
                if (Value == null)
                    _value = new List<T>();
                (Value as List<T>).Add(value);
            }

            public ODataItem Item => _item;
            public Object Value => _value;
            public IReadOnlyList<KeyValuePair<String, Object>> NavigationProperties => _navigationProperties;
        }

        private readonly IEdmModel _edmModel;
        private readonly Db.OeEntitySetMetaAdapterCollection _entitySetMetaAdapters;

        public ResponseReader(IEdmModel edmModel, Db.OeEntitySetMetaAdapterCollection entitySetMetaAdapters)
        {
            _edmModel = edmModel;
            _entitySetMetaAdapters = entitySetMetaAdapters;
        }

        private Object CreateEntity(StackItem stackItem)
        {
            var entry = (ODataResource)stackItem.Item;
            if (entry == null)
                return null;

            var entitySetMetaAdapter = _entitySetMetaAdapters.FindByTypeName(entry.TypeName);
            var entity = OeEntityItem.CreateEntity(entitySetMetaAdapter.EntityType, entry);

            if (stackItem.NavigationProperties.Count > 0)
            {
                PropertyDescriptorCollection clrProperties = TypeDescriptor.GetProperties(entitySetMetaAdapter.EntityType);
                foreach (var navigationProperty in stackItem.NavigationProperties)
                {
                    PropertyDescriptor clrProperty = clrProperties[navigationProperty.Key];
                    clrProperty.SetValue(entity, navigationProperty.Value);
                }
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
        private JObject CreateOpenTypeEntity(ODataResource resource, IReadOnlyList<KeyValuePair<String, Object>> navigationProperties)
        {
            var openType = new JObject();
            foreach (ODataProperty property in resource.Properties.OrderBy(p => p.Name))
                if (property.Value is ODataUntypedValue)
                    openType.Add(property.Name, null);
                else if (property.Value is ODataEnumValue)
                    openType.Add(property.Name, new JRaw(property.Value));
                else
                    openType.Add(property.Name, new JValue(property.Value));

            foreach (KeyValuePair<String, Object> pair in navigationProperties)
            {
                Object value = pair.Value;
                if (value is StackItem)
                {
                    value = CreateEntity((StackItem)value);
                    if (value == null)
                        openType.Add(pair.Key, JValue.CreateNull());
                    else
                        openType.Add(pair.Key, JObject.FromObject(value));
                }
                else
                    openType.Add(pair.Key, JArray.FromObject(value));
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
                                return contextUri.Fragment.Substring(1);
                        }
                        return null;
                    }
            }

            return null;
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
        public IEnumerable<JObject> ReadOpenType(Stream response, Type baseEntityType)
        {
            IODataRequestMessage responseMessage = new OeInMemoryMessage(response, null);
            var settings = new ODataMessageReaderSettings() { Validations = ValidationKinds.None, EnableMessageStreamDisposal = false };
            var messageReader = new ODataMessageReader(responseMessage, settings, _edmModel);

            IEdmEntitySet entitySet = _edmModel.EntityContainer.EntitySets().Single(e => e.Type.AsElementType().FullTypeName() == baseEntityType.FullName);
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
                        stack.Peek().AddLink((ODataNestedResourceInfo)item.Item, item.Value);
                        break;
                }
            }
        }
        private IEnumerable<T> ReadFeedImpl<T>(Stream response, Db.OeEntitySetMetaAdapter entitySetMetaAdatpter)
        {
            IODataRequestMessage responseMessage = new OeInMemoryMessage(response, null);
            var settings = new ODataMessageReaderSettings() { EnableMessageStreamDisposal = false };
            var messageReader = new ODataMessageReader(responseMessage, settings, _edmModel);

            IEdmEntitySet entitySet = _edmModel.EntityContainer.FindEntitySet(entitySetMetaAdatpter.EntitySetName);
            ODataReader reader = messageReader.CreateODataResourceSetReader(entitySet, entitySet.EntityType());

            var stack = new Stack<StackItem>();
            while (reader.Read())
            {
                switch (reader.State)
                {
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
                        stack.Peek().AddLink((ODataNestedResourceInfo)item.Item, item.Value);
                        break;
                }
            }
        }
    }
}
