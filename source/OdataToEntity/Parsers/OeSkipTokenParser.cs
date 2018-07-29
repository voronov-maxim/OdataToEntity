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
    public readonly struct OeSkipTokenNameValue
    {
        public OeSkipTokenNameValue(String name, Object value)
        {
            Name = name;
            Value = value;
        }

        public String Name { get; }
        public Object Value { get; }
    }

    public static class OeSkipTokenParser
    {
        private static readonly ODataMessageReaderSettings ReaderSettings = new ODataMessageReaderSettings() { EnableMessageStreamDisposal = false };
        private static readonly ODataMessageWriterSettings WriterSettings = new ODataMessageWriterSettings() { EnableMessageStreamDisposal = false };

        public static OeSkipTokenNameValue[] CreateNameValues(IEdmModel edmModel, OrderByClause uniqueOrderBy, String skipToken)
        {
            return skipToken == null ? Array.Empty<OeSkipTokenNameValue>() : ParseSkipToken(edmModel, uniqueOrderBy, skipToken);
        }
        public static OePropertyAccessor[] GetAccessors(Expression source, OrderByClause orderByClause)
        {
            var accessors = new List<OePropertyAccessor>();

            var tupleProperty = new Translators.OePropertyTranslator(source);
            Type itemType = OeExpressionHelper.GetCollectionItemType(source.Type);
            ParameterExpression parameter = Expression.Parameter(typeof(Object));
            UnaryExpression instance = Expression.Convert(parameter, itemType);

            while (orderByClause != null)
            {
                var propertyNode = (SingleValuePropertyAccessNode)orderByClause.Expression;
                MemberExpression propertyExpression = tupleProperty.Build(instance, propertyNode.Property);
                if (propertyExpression == null)
                    throw new InvalidOperationException("order by property " + propertyNode.Property.Name + "not found");

                accessors.Add(OePropertyAccessor.CreatePropertyAccessor(propertyNode.Property, propertyExpression, parameter));
                orderByClause = orderByClause.ThenBy;
            }

            return accessors.ToArray();
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
                IEdmEntitySet entitySet = OeEdmClrHelper.GetEntitySet(edmModel, edmType);
                ResourceRangeVariableReferenceNode source = OeEdmClrHelper.CreateRangeVariableReferenceNode(entitySet);
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
        private static KeyValuePair<String, Object>[] GetKeys(OePropertyAccessor[] accessors, Object value)
        {
            var keys = new KeyValuePair<String, Object>[accessors.Length];
            for (int i = 0; i < keys.Length; i++)
                keys[i] = new KeyValuePair<String, Object>(GetPropertyName(accessors[i].EdmProperty), accessors[i].GetValue(value));
            return keys;
        }
        private static IEdmStructuralProperty[] GetEdmProperies(OrderByClause orderByClause)
        {
            var edmProperties = new List<IEdmStructuralProperty>();
            while (orderByClause != null)
            {
                if (orderByClause.Expression is SingleValuePropertyAccessNode propertyNode)
                    edmProperties.Add((IEdmStructuralProperty)propertyNode.Property);
                else
                    throw new NotSupportedException("support only SingleValuePropertyAccessNode");

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
        public static String GetPropertyName(IEdmProperty edmProperty)
        {
            return ((IEdmNamedElement)edmProperty.DeclaringType).Name + "_" + edmProperty.Name;
        }
        public static String GetSkipToken(IEdmModel edmModel, OePropertyAccessor[] accessors, Object value)
        {
            return GetJson(edmModel, GetKeys(accessors, value));
        }
        public static String GetSkipToken(IEdmModel edmModel, IEnumerable<KeyValuePair<String, Object>> keys)
        {
            return GetJson(edmModel, keys);
        }
        private static OeSkipTokenNameValue[] ParseJson(IEdmModel model, String skipToken, IEdmStructuralProperty[] keys)
        {
            var skipTokenNameValues = new OeSkipTokenNameValue[keys.Length];
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(skipToken)))
            {
                IODataRequestMessage requestMessage = new OeInMemoryMessage(stream, null);
                using (ODataMessageReader messageReader = new ODataMessageReader(requestMessage, ReaderSettings, model))
                {
                    var operation = new EdmAction("", "", null);
                    foreach (IEdmStructuralProperty key in keys)
                        operation.AddParameter(GetPropertyName(key), key.Type);

                    ODataParameterReader reader = messageReader.CreateODataParameterReader(operation);
                    int i = 0;
                    while (reader.Read())
                    {
                        Object value = reader.Value;
                        if (value is ODataEnumValue enumValue)
                            value = OeEdmClrHelper.GetValue(model, enumValue);
                        skipTokenNameValues[i++] = new OeSkipTokenNameValue(reader.Name, value);
                    }
                }
            }
            return skipTokenNameValues;
        }
        private static OeSkipTokenNameValue[] ParseSkipToken(IEdmModel edmModel, OrderByClause uniqueOrderBy, String skipToken)
        {
            return ParseJson(edmModel, skipToken, GetEdmProperies(uniqueOrderBy));
        }
    }
}
