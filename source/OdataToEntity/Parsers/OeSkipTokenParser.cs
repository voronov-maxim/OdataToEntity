using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using Microsoft.OData.UriParser.Aggregation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace OdataToEntity.Parsers
{
    public sealed class OeSkipTokenParser
    {
        private static readonly ODataMessageReaderSettings ReaderSettings = new ODataMessageReaderSettings() { EnableMessageStreamDisposal = false };
        private static readonly ODataMessageWriterSettings WriterSettings = new ODataMessageWriterSettings() { EnableMessageStreamDisposal = false };

        public OeSkipTokenParser(IEdmModel edmModel, IEdmEntityType edmType, bool isDatabaseNullHighestValue, OrderByClause uniqueOrderBy)
        {
            EdmModel = edmModel;
            IsDatabaseNullHighestValue = isDatabaseNullHighestValue;
            UniqueOrderBy = uniqueOrderBy;
        }

        private static List<SingleValuePropertyAccessNode> GetOrderByProperties(IEdmModel edmModel, IEdmEntityType edmType, OrderByClause orderByClause, ApplyClause applyClause)
        {
            var keys = new List<SingleValuePropertyAccessNode>();
            GroupByTransformationNode groupByNode;
            if (applyClause != null && (groupByNode = applyClause.Transformations.OfType<GroupByTransformationNode>().SingleOrDefault()) != null)
            {
                foreach (GroupByPropertyNode node in groupByNode.GroupingProperties)
                    if (node.Expression == null)
                        keys.AddRange(node.ChildTransformations.Select(n => (SingleValuePropertyAccessNode)n.Expression));
                    else
                        keys.Add((SingleValuePropertyAccessNode)node.Expression);
            }
            else
            {
                ResourceRangeVariableReferenceNode source = GetResourceRangeNode(edmModel, edmType);
                foreach (IEdmStructuralProperty key in edmType.Key())
                    keys.Add(new SingleValuePropertyAccessNode(source, key));
            }

            if (orderByClause == null)
                return keys;

            for (; orderByClause != null; orderByClause = orderByClause.ThenBy)
            {
                var propertyNode = (SingleValuePropertyAccessNode)orderByClause.Expression;
                int i = keys.FindIndex(p => p.Property == propertyNode.Property);
                if (i >= 0)
                    keys.RemoveAt(i);
            }
            return keys;
        }
        private static ResourceRangeVariableReferenceNode GetResourceRangeNode(IEdmModel edmModel, IEdmEntityType edmType)
        {
            IEdmEntitySet entitySet = null;
            foreach (IEdmEntitySet element in edmModel.EntityContainer.EntitySets())
                if (element.EntityType() == edmType)
                {
                    entitySet = element;
                    break;
                }

            if (entitySet == null)
                throw new InvalidOperationException("IEdmEntitySet not found for IEdmEntityType " + edmType.FullName());

            var entityTypeRef = (IEdmEntityTypeReference)((IEdmCollectionType)entitySet.Type).ElementType;
            var range = new ResourceRangeVariable("", entityTypeRef, entitySet);
            return new ResourceRangeVariableReferenceNode("$it", range);
        }
        internal static OrderByClause GetUniqueOrderBy(IEdmModel edmModel, IEdmEntityType edmType, OrderByClause orderByClause, ApplyClause applyClause)
        {
            if (orderByClause != null && applyClause == null && GetIsKey(edmType, GetEdmProperies(orderByClause)))
                return orderByClause;

            List<SingleValuePropertyAccessNode> orderByProperties = GetOrderByProperties(edmModel, edmType, orderByClause, applyClause);
            if (orderByProperties.Count == 0)
                return orderByClause ?? throw new InvalidOperationException("orderByClause must not null");

            OrderByClause uniqueOrderByClause = null;
            for (int i = orderByProperties.Count - 1; i >= 0; i--)
            {
                ResourceRangeVariableReferenceNode source;
                if (orderByProperties[i].Source is SingleNavigationNode navigationNode)
                    source = (ResourceRangeVariableReferenceNode)navigationNode.Source;
                else
                    source = (ResourceRangeVariableReferenceNode)orderByProperties[i].Source;
                uniqueOrderByClause = new OrderByClause(uniqueOrderByClause, orderByProperties[i], OrderByDirection.Ascending, source.RangeVariable);
            }

            if (orderByClause == null)
                return uniqueOrderByClause;

            var orderByClauses = new Stack<OrderByClause>();
            do
            {
                orderByClauses.Push(orderByClause);
                orderByClause = orderByClause.ThenBy;
            }
            while (orderByClause != null);

            while (orderByClauses.Count > 0)
            {
                orderByClause = orderByClauses.Pop();
                uniqueOrderByClause = new OrderByClause(uniqueOrderByClause, orderByClause.Expression, orderByClause.Direction, orderByClause.RangeVariable);
            }

            return uniqueOrderByClause;
        }
        private KeyValuePair<String, Object>[] GetKeys(Object value)
        {
            var keys = new KeyValuePair<String, Object>[Accessors.Length];
            for (int i = 0; i < keys.Length; i++)
                keys[i] = new KeyValuePair<String, Object>(GetPropertyName(Accessors[i].EdmProperty), Accessors[i].Accessor(value));
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
                    {
                        Object value = key.Value;
                        if (value != null && value.GetType().IsEnum)
                            value = value.ToString();
                        writer.WriteValue(key.Key, value);
                    }
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
        public static String GetPropertyName(Expression expression)
        {
            MemberExpression propertyExpression;
            if (expression is UnaryExpression unaryExpression)
                propertyExpression = (MemberExpression)unaryExpression.Operand;
            else
                propertyExpression = (MemberExpression)expression;
            return propertyExpression.Member.DeclaringType.Name + "_" + propertyExpression.Member.Name;
        }
        public static String GetPropertyName(IEdmProperty edmProperty) => ((IEdmNamedElement)edmProperty.DeclaringType).Name + "_" + edmProperty.Name;
        public String GetSkipToken(Object value)
        {
            String json = GetJson(EdmModel, GetKeys(value));
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        }
        public static String GetSkipToken(IEdmModel edmModel, IEnumerable<KeyValuePair<String, Object>> keys)
        {
            String json = GetJson(edmModel, keys);
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
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
                        operation.AddParameter(GetPropertyName(key), key.Type);

                    ODataParameterReader reader = messageReader.CreateODataParameterReader(operation);
                    while (reader.Read())
                    {
                        Object value = reader.Value;
                        if (value is ODataEnumValue enumValue)
                            value = OeEdmClrHelper.GetValue(model, enumValue);
                        yield return new KeyValuePair<String, Object>(reader.Name, value);
                    }
                }
            }
        }
        public IEnumerable<KeyValuePair<String, Object>> ParseSkipToken(String skipToken)
        {
            String json = Encoding.UTF8.GetString(Convert.FromBase64String(skipToken));
            return ParseJson(EdmModel, json, GetEdmProperies(UniqueOrderBy));
        }

        public OePropertyAccessor[] Accessors { get; set; }
        public IEdmModel EdmModel { get; }
        public bool IsDatabaseNullHighestValue { get; }
        public OrderByClause UniqueOrderBy { get; }
    }
}
