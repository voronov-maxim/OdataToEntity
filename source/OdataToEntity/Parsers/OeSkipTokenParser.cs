using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using OdataToEntity.ModelBuilder;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace OdataToEntity.Parsers
{
    public sealed class OeSkipTokenParser
    {
        private static readonly ODataMessageReaderSettings ReaderSettings = new ODataMessageReaderSettings() { EnableMessageStreamDisposal = false };
        private static readonly ODataMessageWriterSettings WriterSettings = new ODataMessageWriterSettings() { EnableMessageStreamDisposal = false };

        private readonly PropertyInfo[] _clrProperties;
        private readonly IEdmModel _edmModel;
        private readonly IEdmStructuralProperty[] _edmProperties;
        private readonly OrderByClause _orderByClause;

        public OeSkipTokenParser(IEdmModel edmModel, IEdmEntityType edmType)
        {
            _orderByClause = GetUniqueOrderBy(edmModel, edmType, null);
            _edmProperties = GetEdmProperies(_orderByClause);
            _clrProperties = GetClrProperies(edmModel.GetClrType(edmType), _edmProperties);
        }
        public OeSkipTokenParser(IEdmModel edmModel, IEdmEntityType edmType, OrderByClause orderByClause)
        {
            _edmModel = edmModel;
            _orderByClause = orderByClause;

            _edmProperties = GetEdmProperies(orderByClause);
            if (!GetIsKey(edmType, _edmProperties))
            {
                _orderByClause = GetUniqueOrderBy(edmModel, edmType, orderByClause);
                _edmProperties = GetEdmProperies(_orderByClause);
            }

            _clrProperties = GetClrProperies(edmModel.GetClrType(edmType), _edmProperties);
        }

        public OrderByClause GetUniqueOrderBy(IEdmModel edmModel, IEdmEntityType edmType, OrderByClause orderByClause)
        {
            IEdmEntitySet entitySet = null;
            foreach (IEdmEntitySet element in edmModel.EntityContainer.EntitySets())
                if (element.EntityType() == edmType)
                {
                    entitySet = element;
                    break;
                }

            OrderByClause uniqueOrderByClause = null;
            foreach (IEdmStructuralProperty keyProperty in edmType.Key().Reverse())
            {
                var entityTypeRef = (IEdmEntityTypeReference)((IEdmCollectionType)entitySet.Type).ElementType;
                var range = new ResourceRangeVariable("", entityTypeRef, entitySet);
                var source = new ResourceRangeVariableReferenceNode("$it", range);
                var node = new SingleValuePropertyAccessNode(source, keyProperty);
                uniqueOrderByClause = new OrderByClause(uniqueOrderByClause, node, OrderByDirection.Ascending, source.RangeVariable);
            }

            var orderByClauses = new Stack<OrderByClause>();
            while (orderByClause != null)
            {
                orderByClauses.Push(orderByClause);
                orderByClause = orderByClause.ThenBy;
            }
            while (orderByClauses.Count > 0)
            {
                orderByClause = orderByClauses.Pop();
                uniqueOrderByClause = new OrderByClause(uniqueOrderByClause, orderByClause.Expression, orderByClause.Direction, orderByClause.RangeVariable);
            }

            return uniqueOrderByClause;
        }
        private static PropertyInfo[] GetClrProperies(Type ItemType, IEdmStructuralProperty[] edmProperties)
        {
            var clrProperties = new PropertyInfo[edmProperties.Length];
            for (int i = 0; i < clrProperties.Length; i++)
                clrProperties[i] = ItemType.GetPropertyIgnoreCase(edmProperties[i].Name);
            return clrProperties;
        }
        private PropertyInfo GetClrProperty(String propertyName)
        {
            foreach (PropertyInfo clrPropery in _clrProperties)
                if (String.Compare(clrPropery.Name, propertyName, StringComparison.OrdinalIgnoreCase) == 0)
                    return clrPropery;

            throw new InvalidOperationException("property name " + propertyName + " not found in OrderByClause");
        }
        private KeyValuePair<String, Object>[] GetKeys(ODataResource entry)
        {
            var keys = new KeyValuePair<String, Object>[_edmProperties.Length];
            for (int i = 0; i < _edmProperties.Length; i++)
                foreach (ODataProperty odataProperty in entry.Properties)
                    if (String.CompareOrdinal(odataProperty.Name, _edmProperties[i].Name) == 0)
                        keys[i] = new KeyValuePair<String, Object>(_edmProperties[i].Name, odataProperty.Value);
            return keys;
        }
        private static IEdmStructuralProperty[] GetEdmProperies(OrderByClause orderByClause)
        {
            var edmProperties = new List<IEdmStructuralProperty>();
            while (orderByClause != null)
            {
                var propertyNode = orderByClause.Expression as SingleValuePropertyAccessNode;
                if (propertyNode == null)
                    throw new NotSupportedException("support only SingleValuePropertyAccessNode");

                edmProperties.Add((IEdmStructuralProperty)propertyNode.Property);
                orderByClause = orderByClause.ThenBy;
            }
            return edmProperties.ToArray();
        }
        public static String GetJson(IEdmModel model, IEnumerable<KeyValuePair<String, Object>> keys)
        {
            using (var stream = new MemoryStream())
            {
                IODataRequestMessage requestMessage = new OeInMemoryMessage(stream, null);
                using (ODataMessageWriter messageWriter = new ODataMessageWriter(requestMessage, WriterSettings, model))
                {
                    ODataParameterWriter writer = messageWriter.CreateODataParameterWriter(null);
                    writer.WriteStart();
                    foreach (KeyValuePair<String, Object> key in keys)
                        writer.WriteValue(key.Key, key.Value);
                    writer.WriteEnd();
                }

                return Encoding.UTF8.GetString(stream.GetBuffer(), 0, (int)stream.Length);
            }
        }
        private static bool GetIsKey(IEdmEntityType edmType, IEdmStructuralProperty[] edmProperties)
        {
            int i = 0;
            foreach (IEdmStructuralProperty edmProperty in edmType.Key())
            {
                i = Array.IndexOf(edmProperties, edmProperty, i);
                if (i < 0)
                    return false;
                i++;
            }
            return true;
        }
        public String GetSkipToken(ODataResource entry)
        {
            return GetJson(_edmModel, GetKeys(entry));
        }
        public static IEnumerable<KeyValuePair<String, Object>> ParseJson(IEdmModel model, String skipToken, IEnumerable<IEdmStructuralProperty> keys)
        {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(skipToken)))
            {
                IODataRequestMessage requestMessage = new OeInMemoryMessage(stream, null);
                using (ODataMessageReader messageReader = new ODataMessageReader(requestMessage, ReaderSettings, model))
                {
                    var operation = new EdmAction("", "", null);
                    foreach (IEdmStructuralProperty key in keys)
                        operation.AddParameter(key.Name, key.Type);

                    ODataParameterReader reader = messageReader.CreateODataParameterReader(operation);
                    while (reader.Read())
                        yield return new KeyValuePair<String, Object>(reader.Name, reader.Value);
                }
            }
        }
        public KeyValuePair<PropertyInfo, Object>[] ParseSkipToken(String skipToken)
        {
            var keys = new KeyValuePair<PropertyInfo, Object>[_clrProperties.Length];
            String json = Encoding.UTF8.GetString(Convert.FromBase64String(skipToken));
            int i = 0;
            foreach (KeyValuePair<String, Object> key in ParseJson(_edmModel, json, _edmProperties))
                keys[i++] = new KeyValuePair<PropertyInfo, Object>(GetClrProperty(key.Key), key.Value);
            return keys;
        }

        public OrderByClause UniqueOrderBy => _orderByClause;
    }
}
