using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace OdataToEntity.Cache.UriCompare
{
    public readonly struct OeCacheComparerParameterValues
    {
        private sealed class SkipTokenMarker : IEdmTypeReference
        {
            public static readonly SkipTokenMarker Instance = new SkipTokenMarker();

            private SkipTokenMarker()
            {
            }

            public bool IsNullable => throw new NotImplementedException();
            public IEdmType Definition => throw new NotImplementedException();
        }

        private readonly IReadOnlyDictionary<ConstantNode, OeQueryCacheDbParameterDefinition>? _constantToParameterMapper;
        private readonly List<OeQueryCacheDbParameterValue> _parameterValues;

        public OeCacheComparerParameterValues(IReadOnlyDictionary<ConstantNode, OeQueryCacheDbParameterDefinition>? constantToParameterMapper)
        {
            _constantToParameterMapper = constantToParameterMapper;
            _parameterValues = new List<OeQueryCacheDbParameterValue>(_constantToParameterMapper == null ? 0 : _constantToParameterMapper.Count);
        }

        public void AddParameter(ConstantNode keyConstantNode, ConstantNode parameterConstanNode)
        {
            if (_constantToParameterMapper == null)
                return;

            OeQueryCacheDbParameterDefinition parameterDefinition = _constantToParameterMapper[keyConstantNode];
            if (parameterConstanNode.Value == null)
                _parameterValues.Add(new OeQueryCacheDbParameterValue(parameterDefinition.ParameterName, null));
            else
            {
                if (parameterConstanNode.Value.GetType() != parameterDefinition.ParameterType)
                {
                    ConstantExpression oldConstant = Expression.Constant(parameterConstanNode.Value, parameterConstanNode.Value.GetType());
                    ConstantExpression newConstant = Parsers.OeExpressionHelper.ConstantChangeType(oldConstant, parameterDefinition.ParameterType);
                    _parameterValues.Add(new OeQueryCacheDbParameterValue(parameterDefinition.ParameterName, newConstant.Value));
                }
                else
                    _parameterValues.Add(new OeQueryCacheDbParameterValue(parameterDefinition.ParameterName, parameterConstanNode.Value));
            }
        }
        public void AddSkipParameter(long value, ODataPath path)
        {
            if (_constantToParameterMapper == null)
                return;

            String resourcePath = GetSegmentResourcePathSkip(path);
            foreach (KeyValuePair<ConstantNode, OeQueryCacheDbParameterDefinition> pair in _constantToParameterMapper)
                if (pair.Value.ParameterType == typeof(int) && pair.Key.LiteralText == resourcePath)
                {
                    _parameterValues.Add(new OeQueryCacheDbParameterValue(pair.Value.ParameterName, (int)value));
                    return;
                }

            throw new InvalidOperationException("skip not found");
        }
        public void AddSkipTokenParameter(Object value, String propertyName)
        {
            if (_constantToParameterMapper == null)
                return;

            foreach (KeyValuePair<ConstantNode, OeQueryCacheDbParameterDefinition> pair in _constantToParameterMapper)
                if (pair.Key.TypeReference == SkipTokenMarker.Instance && String.CompareOrdinal(pair.Key.LiteralText, propertyName) == 0)
                    _parameterValues.Add(new OeQueryCacheDbParameterValue(pair.Value.ParameterName, value));
        }
        public void AddTopParameter(long value, ODataPath path)
        {
            if (_constantToParameterMapper == null)
                return;

            String resourcePath = GetSegmentResourcePathTop(path);
            foreach (KeyValuePair<ConstantNode, OeQueryCacheDbParameterDefinition> pair in _constantToParameterMapper)
                if (pair.Value.ParameterType == typeof(int) && pair.Key.LiteralText == resourcePath)
                {
                    for (int i = 0; i < _parameterValues.Count; i++)
                        if (String.CompareOrdinal(_parameterValues[i].ParameterName, pair.Value.ParameterName) == 0)
                        {
                            Object? parameterValue = _parameterValues[i].ParameterValue;
                            if (parameterValue == null)
                                throw new InvalidOperationException("Top parameter " + _parameterValues[i].ParameterName + " is null");

                            if (value < (int)parameterValue)
                                _parameterValues[i] = new OeQueryCacheDbParameterValue(pair.Value.ParameterName, (int)value);
                            return;
                        }

                    _parameterValues.Add(new OeQueryCacheDbParameterValue(pair.Value.ParameterName, (int)value));
                    return;
                }

            throw new InvalidOperationException("top not found");
        }
        public static ConstantNode CreateSkipConstantNode(int skip, ODataPath path)
        {
            return new ConstantNode(skip, GetSegmentResourcePathSkip(path));
        }
        public static ConstantNode CreateSkipTokenConstantNode(Object value, String propertyName)
        {
            return new ConstantNode(value, propertyName, SkipTokenMarker.Instance);
        }
        public static ConstantNode CreateTopConstantNode(int top, ODataPath path)
        {
            return new ConstantNode(top, GetSegmentResourcePathTop(path));
        }
        private static String GetSegmentResourcePathSkip(ODataPath path)
        {
            return GetSegmentResourcePath(path, "skip");
        }
        private static String GetSegmentResourcePathTop(ODataPath path)
        {
            return GetSegmentResourcePath(path, "top");
        }
        private static String GetSegmentResourcePath(ODataPath path, String skipOrTop)
        {
            var stringBuilder = new StringBuilder();
            foreach (ODataPathSegment pathSegment in path)
            {
                if (stringBuilder.Length > 0)
                    stringBuilder.Append('/');

                if (pathSegment is EntitySetSegment entitySetSegment)
                    stringBuilder.Append(entitySetSegment.EntitySet.Name);
                else if (pathSegment is NavigationPropertySegment navigationPropertySegment)
                    stringBuilder.Append(navigationPropertySegment.NavigationProperty.Name);
                else if (pathSegment is KeySegment)
                {
                    stringBuilder.Append(pathSegment.Identifier);
                    stringBuilder.Append("()");
                }
                else if (pathSegment is CountSegment)
                    stringBuilder.Append(pathSegment.Identifier);
                else if (pathSegment is FilterSegment)
                    stringBuilder.Append(pathSegment.Identifier);
                else
                    throw new InvalidOperationException("unknown ODataPathSegment " + pathSegment.GetType().ToString());
            }
            return stringBuilder.Append(':').Append(skipOrTop).ToString();
        }

        public IReadOnlyList<OeQueryCacheDbParameterValue> ParameterValues => _parameterValues;
    }
}
