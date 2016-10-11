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

namespace OdataToEntity.Parsers
{
    public sealed class OeResponseReader
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
                var link = (ODataNestedResourceInfo)Item;
                if (link.IsCollection.GetValueOrDefault())
                    AddToList((dynamic)value);
                else
                    _value = value;
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

        public OeResponseReader(IEdmModel edmModel, Db.OeEntitySetMetaAdapterCollection entitySetMetaAdapters)
        {
            _edmModel = edmModel;
            _entitySetMetaAdapters = entitySetMetaAdapters;
        }

        private Object CreateEntity(StackItem stackItem)
        {
            var entry = (ODataResource)stackItem.Item;
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
        private JObject CreateOpenTypeEntity(StackItem stackItem)
        {
            var openType = new JObject();
            var entry = (ODataResource)stackItem.Item;
            foreach (ODataProperty property in entry.Properties.OrderBy(p => p.Name))
                if (property.Value is ODataUntypedValue)
                    openType.Add(property.Name, null);
                else if (property.Value is ODataEnumValue)
                    openType.Add(property.Name, new JRaw(property.Value));
                else
                    openType.Add(property.Name, new JValue(property.Value));
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
            IODataRequestMessage responseMessage = new OeInMemoryMessage(response, null);
            var settings = new ODataMessageReaderSettings() { Validations = ValidationKinds.None, EnableMessageStreamDisposal = false };
            var messageReader = new ODataMessageReader(responseMessage, settings, _edmModel);

            IEdmEntitySet entitySet = _edmModel.EntityContainer.FindEntitySet("OrderItems");
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
                        var entity = CreateOpenTypeEntity(stackItem);
                        if (stack.Count == 0)
                            yield return entity;
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
        private IEnumerable<T> ReadFeedImpl<T>(Stream response, Db.OeEntitySetMetaAdapter entitySetMetaAdatpter)
        {
            var zzz = new StreamReader(response).ReadToEnd();
            response.Position = 0;

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
