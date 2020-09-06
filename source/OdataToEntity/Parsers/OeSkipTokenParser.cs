using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using Microsoft.OData.UriParser.Aggregation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
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
        private const String RestCountName = "<>$restCount";

        private static readonly ODataMessageReaderSettings ReaderSettings = new ODataMessageReaderSettings() { EnableMessageStreamDisposal = false };
        private static readonly ODataMessageWriterSettings WriterSettings = new ODataMessageWriterSettings() { EnableMessageStreamDisposal = false };

        public static OePropertyAccessor[] GetAccessors(Expression source, OrderByClause orderByClause, Translators.OeJoinBuilder joinBuilder)
        {
            var accessors = new List<OePropertyAccessor>();

            ParameterExpression parameter = Expression.Parameter(typeof(Object));
            UnaryExpression instance = Expression.Convert(parameter, OeExpressionHelper.GetCollectionItemType(source.Type));
            while (orderByClause != null)
            {
                MemberExpression propertyExpression = Translators.OeOrderByTranslator.GetPropertyExpression(joinBuilder, source, instance, orderByClause.Expression);
                IEdmStructuralProperty edmProperty = GetEdmProperty(orderByClause.Expression, propertyExpression.Type);
                OePropertyAccessor accessor;
                if (typeof(OeIndexerProperty).IsAssignableFrom(propertyExpression.Expression!.Type))
                {
                    InterfaceMapping interfaceMapping = propertyExpression.Expression!.Type.GetInterfaceMap(typeof(OeIndexerProperty));
                    MethodCallExpression expression = Expression.Call(propertyExpression.Expression!, interfaceMapping.TargetMethods[0], Expression.Constant(edmProperty.Name));
                    accessor = OePropertyAccessor.CreatePropertyAccessor(edmProperty, expression, parameter, true);
                }
                else
                    accessor = OePropertyAccessor.CreatePropertyAccessor(edmProperty, propertyExpression, parameter, true);
                accessors.Add(accessor);
                orderByClause = orderByClause.ThenBy;
            }

            return accessors.ToArray();
        }
        public static IEdmStructuralProperty[] GetEdmProperies(OrderByClause orderByClause)
        {
            var edmProperties = new List<IEdmStructuralProperty>();
            while (orderByClause != null)
            {
                edmProperties.Add(GetEdmProperty(orderByClause.Expression, typeof(Decimal)));
                orderByClause = orderByClause.ThenBy;
            }
            return edmProperties.ToArray();
        }
        public static IEdmStructuralProperty GetEdmProperty(SingleValueNode sortProperty, Type propertyType)
        {
            if (sortProperty is SingleValuePropertyAccessNode propertyNode)
                return (IEdmStructuralProperty)propertyNode.Property;
            else if (sortProperty is SingleValueOpenPropertyAccessNode openPropertyNode)
            {
                IEdmTypeReference typeReference = OeEdmClrHelper.GetEdmTypeReference(propertyType);
                return new EdmStructuralProperty(ModelBuilder.PrimitiveTypeHelper.TupleEdmType, openPropertyNode.Name, typeReference);
            }

            throw new InvalidOperationException("Unknown type order by expression " + sortProperty.GetType().Name);
        }
        private static KeyValuePair<String, Object?>[] GetKeys(OePropertyAccessor[] accessors, Object value)
        {
            var keys = new KeyValuePair<String, Object?>[accessors.Length];
            for (int i = 0; i < keys.Length; i++)
                keys[i] = new KeyValuePair<String, Object?>(GetPropertyName(accessors[i].EdmProperty), accessors[i].GetValue(value));
            return keys;
        }
        public static String GetJson(IEdmModel edmModel, IEnumerable<KeyValuePair<String, Object?>> keys)
        {
            using (var stream = new MemoryStream())
            {
                IODataRequestMessage requestMessage = new Infrastructure.OeInMemoryMessage(stream, null);
                using (ODataMessageWriter messageWriter = new ODataMessageWriter(requestMessage, WriterSettings, edmModel))
                {
                    ODataParameterWriter writer = messageWriter.CreateODataParameterWriter(null);
                    writer.WriteStart();
                    foreach (KeyValuePair<String, Object?> key in keys)
                    {
                        Object? value = key.Value;
                        if (value != null && value.GetType().IsEnum)
                            value = value.ToString()!;
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
        private static List<SingleValuePropertyAccessNode> GetOrderByProperties(IEdmEntitySetBase entitySet, OrderByClause? orderByClause, ApplyClause? applyClause)
        {
            var keys = new List<SingleValuePropertyAccessNode>();
            GroupByTransformationNode? groupByNode;
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
                ResourceRangeVariableReferenceNode source = OeEdmClrHelper.CreateRangeVariableReferenceNode(entitySet);
                foreach (IEdmStructuralProperty key in entitySet.EntityType().Key())
                    keys.Add(new SingleValuePropertyAccessNode(source, key));
            }

            if (orderByClause == null)
                return keys;

            for (; orderByClause != null; orderByClause = orderByClause.ThenBy)
                if (orderByClause.Expression is SingleValuePropertyAccessNode propertyNode)
                {
                    int i = keys.FindIndex(p => p.Property == propertyNode.Property);
                    if (i >= 0)
                        keys.RemoveAt(i);

                }
            return keys;
        }
        public static String GetPropertyName(IEdmProperty edmProperty)
        {
            return ((IEdmNamedElement)edmProperty.DeclaringType).Name + "_" + edmProperty.Name;
        }
        public static String GetSkipToken(IEdmModel edmModel, OePropertyAccessor[] accessors, Object value, int? restCount)
        {
            KeyValuePair<String, Object?>[] keys = GetKeys(accessors, value);
            if (restCount.GetValueOrDefault() > 0)
            {
                Array.Resize(ref keys, keys.Length + 1);
                keys[keys.Length - 1] = new KeyValuePair<String, Object?>(RestCountName, restCount.GetValueOrDefault());
            }

            return GetJson(edmModel, keys);
        }
        public static String GetSkipToken(IEdmModel edmModel, ICollection<KeyValuePair<String, Object?>> keys, int? restCount)
        {
            if (restCount.GetValueOrDefault() > 0)
            {
                var keyArray = new KeyValuePair<String, Object?>[keys.Count + 1];
                int i = 0;
                foreach (KeyValuePair<String, Object?> key in keys)
                    keyArray[i++] = key;
                keyArray[keyArray.Length - 1] = new KeyValuePair<String, Object?>(RestCountName, restCount.GetValueOrDefault());

                return GetJson(edmModel, keyArray);
            }

            return GetJson(edmModel, keys);
        }
        internal static OrderByClause GetUniqueOrderBy(IEdmEntitySetBase entitySet, OrderByClause orderByClause, ApplyClause? applyClause)
        {
            if (orderByClause != null && applyClause == null && GetIsKey(entitySet.EntityType(), GetEdmProperies(orderByClause)))
                return orderByClause;

            List<SingleValuePropertyAccessNode> orderByProperties = GetOrderByProperties(entitySet, orderByClause, applyClause);
            if (orderByProperties.Count == 0)
                return orderByClause ?? throw new InvalidOperationException(nameof(orderByClause) + " parameter must contains orderable properties");

            OrderByClause? uniqueOrderByClause = null;
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
                return uniqueOrderByClause!;

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

            return uniqueOrderByClause!;
        }
        private static OeSkipTokenNameValue[] ParseJson(IEdmModel edmModel, String skipToken, IEdmStructuralProperty[] keys, out int? restCount)
        {
            restCount = null;
            var skipTokenNameValues = new OeSkipTokenNameValue[keys.Length];
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(skipToken)))
            {
                IODataRequestMessage requestMessage = new Infrastructure.OeInMemoryMessage(stream, null);
                using (ODataMessageReader messageReader = new ODataMessageReader(requestMessage, ReaderSettings, edmModel))
                {
                    var operation = new EdmAction("", "", null);
                    foreach (IEdmStructuralProperty key in keys)
                        operation.AddParameter(GetPropertyName(key), key.Type);
                    operation.AddParameter(RestCountName, OeEdmClrHelper.GetEdmTypeReference(edmModel, typeof(int?)));

                    ODataParameterReader reader = messageReader.CreateODataParameterReader(operation);
                    int i = 0;
                    while (reader.Read())
                    {
                        Object value = reader.Value;
                        if (value is ODataEnumValue enumValue)
                            value = OeEdmClrHelper.GetValue(edmModel, enumValue);

                        if (reader.Name == RestCountName)
                            restCount = (int)value;
                        else
                            skipTokenNameValues[i++] = new OeSkipTokenNameValue(reader.Name, value);
                    }
                }
            }
            return skipTokenNameValues;
        }
        public static OeSkipTokenNameValue[] ParseSkipToken(IEdmModel edmModel, OrderByClause uniqueOrderBy, String skipToken, out int? restCount)
        {
            return ParseJson(edmModel, skipToken, GetEdmProperies(uniqueOrderBy), out restCount);
        }
    }
}
