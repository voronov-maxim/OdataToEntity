using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace OdataToEntity.Parsers
{
    public struct OeParseNavigationSegment
    {
        private readonly FilterClause _filter;
        private readonly NavigationPropertySegment _navigationSegment;

        public OeParseNavigationSegment(NavigationPropertySegment navigationSegment, FilterClause filter)
        {
            _navigationSegment = navigationSegment;
            _filter = filter;
        }

        public FilterClause Filter => _filter;
        public NavigationPropertySegment NavigationSegment => _navigationSegment;
    }

    public sealed class OeParseUriContext
    {
        private sealed class SourceVisitor : ExpressionVisitor
        {
            private readonly IQueryable _query;

            private SourceVisitor(IQueryable query)
            {
                _query = query;
            }

            public static Expression Translate(IQueryable query, Expression expression)
            {
                var visitor = new SourceVisitor(query);
                return visitor.Visit(expression);
            }
            protected override Expression VisitConstant(ConstantExpression node)
            {
                if (node.Type.GetTypeInfo().IsGenericType)
                {
                    Type[] args = node.Type.GetTypeInfo().GetGenericArguments();
                    if (args.Length == 1 && args[0] == _query.ElementType)
                        return _query.Expression;
                }
                return base.VisitConstant(node);
            }
        }

        private readonly IEdmModel _edmModel;
        private readonly IEdmEntitySetBase _entitySet;
        private readonly bool _isCountSegment;
        private readonly ODataUri _odataUri;
        private readonly IReadOnlyList<OeParseNavigationSegment> _parseNavigationSegments;

        public OeParseUriContext(IEdmModel edmModel, ODataUri odataUri, IEdmEntitySetBase entitySet, IReadOnlyList<OeParseNavigationSegment> parseNavigationSegments, bool isCountSegment)
        {
            _edmModel = edmModel;
            _odataUri = odataUri;
            _entitySet = entitySet;
            _parseNavigationSegments = parseNavigationSegments;
            _isCountSegment = isCountSegment;
        }

        private OeEntryFactory CreateEntryFactory(OeExpressionBuilder expressionBuilder)
        {
            IEdmEntitySetBase entitySet = EntitySet;
            if (expressionBuilder.EntityType != EntitySetAdapter.EntityType)
            {
                String typeName = expressionBuilder.EntityType.FullName;
                Db.OeDataAdapter dataAdapter = EntitySetAdapter.DataAdapter;
                Db.OeEntitySetMetaAdapter entitySetMetaAdapter = dataAdapter.EntitySetMetaAdapters.FindByTypeName(typeName);
                if (entitySetMetaAdapter != null)
                    entitySet = EdmModel.FindDeclaredEntitySet(entitySetMetaAdapter.EntitySetName);
            }

            OeEntryFactory entryFactory = expressionBuilder.CreateEntryFactory(entitySet);
            entryFactory.CountOption = ODataUri.QueryCount;
            return entryFactory;
        }
        public Expression CreateExpression(IQueryable query, OeConstantToVariableVisitor constantToVariableVisitor)
        {
            Expression expression;
            var expressionBuilder = new OeExpressionBuilder(EdmModel, EntitySetAdapter.EntityType);

            expression = Expression.Constant(null, typeof(IEnumerable<>).MakeGenericType(EntitySetAdapter.EntityType));
            expression = expressionBuilder.ApplyNavigation(expression, ParseNavigationSegments);
            expression = expressionBuilder.ApplyFilter(expression, ODataUri.Filter);
            expression = expressionBuilder.ApplyAggregation(expression, ODataUri.Apply);
            expression = expressionBuilder.ApplySelect(expression, ODataUri.SelectAndExpand, ODataUri.Path, Headers.MetadataLevel);
            expression = expressionBuilder.ApplyOrderBy(expression, ODataUri.OrderBy);
            expression = expressionBuilder.ApplySkip(expression, ODataUri.Skip, ODataUri.Path);
            expression = expressionBuilder.ApplyTake(expression, ODataUri.Top, ODataUri.Path);
            expression = expressionBuilder.ApplyCount(expression, IsCountSegment);

            if (!IsCountSegment)
                EntryFactory = CreateEntryFactory(expressionBuilder);

            expression = constantToVariableVisitor.Translate(expression, expressionBuilder.Constants);
            return SourceVisitor.Translate(query, expression);
        }

        public IReadOnlyDictionary<ConstantNode, Db.OeQueryCacheDbParameterDefinition> ConstantToParameterMapper { get; set; }
        public IEdmModel EdmModel => _edmModel;
        public IEdmEntitySetBase EntitySet => _entitySet;
        public Db.OeEntitySetAdapter EntitySetAdapter { get; set; }
        public OeEntryFactory EntryFactory { get; set; }
        public OeRequestHeaders Headers { get; set; }
        public bool IsCountSegment => _isCountSegment;
        public ODataUri ODataUri => _odataUri;
        public IReadOnlyList<Db.OeQueryCacheDbParameterValue> ParameterValues { get; set; }
        public IReadOnlyList<OeParseNavigationSegment> ParseNavigationSegments => _parseNavigationSegments;
    }
}
