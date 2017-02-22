using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace OdataToEntity.Parsers.UriCompare
{
    public struct OeODataUriComparerParameterValues
    {
        private readonly IReadOnlyDictionary<ConstantNode, Db.OeQueryCacheDbParameterDefinition> _constantToParameterMapper;
        private readonly List<Db.OeQueryCacheDbParameterValue> _parameterValues;

        public OeODataUriComparerParameterValues(IReadOnlyDictionary<ConstantNode, Db.OeQueryCacheDbParameterDefinition> constantToParameterMapper)
        {
            _constantToParameterMapper = constantToParameterMapper;
            _parameterValues = new List<Db.OeQueryCacheDbParameterValue>(_constantToParameterMapper.Count);
        }

        public void AddParameter(ConstantNode keyConstantNode, ConstantNode parameterConstanNode)
        {
            Db.OeQueryCacheDbParameterDefinition parameterDefinition = _constantToParameterMapper[keyConstantNode];
            if (parameterConstanNode.Value == null)
                _parameterValues.Add(new Db.OeQueryCacheDbParameterValue(parameterDefinition.ParameterName, null));
            else
            {
                if (parameterConstanNode.Value.GetType() != parameterDefinition.ParameterType)
                {
                    ConstantExpression oldConstant = Expression.Constant(parameterConstanNode.Value, parameterConstanNode.Value.GetType());
                    ConstantExpression newConstant = OeExpressionHelper.ConstantChangeType(oldConstant, parameterDefinition.ParameterType);
                    _parameterValues.Add(new Db.OeQueryCacheDbParameterValue(parameterDefinition.ParameterName, newConstant.Value));
                }
                else
                    _parameterValues.Add(new Db.OeQueryCacheDbParameterValue(parameterDefinition.ParameterName, parameterConstanNode.Value));
            }
        }
        public void AddSkipParameter(long value, ODataPath path)
        {
            String resourcePath = GetSegmentResourcePathSkip(path);
            foreach (KeyValuePair<ConstantNode, Db.OeQueryCacheDbParameterDefinition> pair in _constantToParameterMapper)
                if (pair.Value.ParameterType == typeof(int) && pair.Key.LiteralText == resourcePath)
                {
                    _parameterValues.Add(new Db.OeQueryCacheDbParameterValue(pair.Value.ParameterName, (int)value));
                    return;
                }

            throw new InvalidOperationException("skip not found");
        }
        public void AddTopParameter(long value, ODataPath path)
        {
            String resourcePath = GetSegmentResourcePathTop(path);
            foreach (KeyValuePair<ConstantNode, Db.OeQueryCacheDbParameterDefinition> pair in _constantToParameterMapper)
                if (pair.Value.ParameterType == typeof(int) && pair.Key.LiteralText == resourcePath)
                {
                    _parameterValues.Add(new Db.OeQueryCacheDbParameterValue(pair.Value.ParameterName, (int)value));
                    return;
                }

            throw new InvalidOperationException("top not found");
        }
        public static ConstantNode CreateSkipConstantNode(int skip, ODataPath path)
        {
            return new ConstantNode(skip, GetSegmentResourcePathSkip(path));
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

                if (pathSegment is EntitySetSegment)
                    stringBuilder.Append((pathSegment as EntitySetSegment).EntitySet.Name);
                else if (pathSegment is NavigationPropertySegment)
                    stringBuilder.Append((pathSegment as NavigationPropertySegment).NavigationProperty.Name);
                else
                    throw new InvalidOperationException("unknown ODataPathSegment " + pathSegment.GetType().ToString());
            }
            return stringBuilder.Append(':').Append(skipOrTop).ToString();
        }

        public IReadOnlyList<Db.OeQueryCacheDbParameterValue> ParameterValues => _parameterValues;
    }
}
