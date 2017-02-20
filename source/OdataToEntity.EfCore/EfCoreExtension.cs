using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using OdataToEntity.Parsers;
using Remotion.Linq;
using Remotion.Linq.Parsing.ExpressionVisitors.Transformation;
using Remotion.Linq.Parsing.ExpressionVisitors.TreeEvaluation;
using Remotion.Linq.Parsing.Structure;
using Remotion.Linq.Parsing.Structure.ExpressionTreeProcessors;
using Remotion.Linq.Parsing.Structure.NodeTypeProviders;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace OdataToEntity.EfCore
{
    internal static class EfCoreExtension
    {
        private sealed class ApiCompilationFilter : EvaluatableExpressionFilterBase { }

        private sealed class ParameterVisitor : ExpressionVisitor
        {
            private int _index;
            private readonly List<KeyValuePair<String, Object>> _parameters;

            public ParameterVisitor()
            {
                _parameters = new List<KeyValuePair<String, Object>>();
            }

            protected override Expression VisitMember(MemberExpression node)
            {
                if (node.Expression.Type.Name.Contains("Tuple"))
                {
                    String paremeterName = "__p_" + _index++.ToString();
                    _parameters.Add(new KeyValuePair<String, Object>(paremeterName, "Order 1"));
                    return Expression.Parameter(node.Type, paremeterName);
                }
                return base.VisitMember(node);
            }
            protected override Expression VisitConstant(ConstantExpression node)
            {
                return base.VisitConstant(node);
            }

            public IReadOnlyList<KeyValuePair<String, Object>> Parameters => _parameters;
        }

        public static Func<QueryContext, IAsyncEnumerable<Object>> CreateAsyncQueryExecutor(this DbContext dbContext, Expression expression)
        {
            QueryModel queryModel = CreateQueryModel(dbContext, expression);
            var queryCompilationContextFactory = dbContext.GetService<IQueryCompilationContextFactory>();
            return queryCompilationContextFactory.Create(true).CreateQueryModelVisitor().CreateAsyncQueryExecutor<Object>(queryModel);
        }
        public static Func<QueryContext, IEnumerable<Object>> CreateQueryExecutor(this DbContext dbContext, Expression expression)
        {
            QueryModel queryModel = CreateQueryModel(dbContext, expression);
            var queryCompilationContextFactory = dbContext.GetService<IQueryCompilationContextFactory>();
            return queryCompilationContextFactory.Create(false).CreateQueryModelVisitor().CreateQueryExecutor<Object>(queryModel);
        }
        private static QueryModel CreateQueryModel(this DbContext dbContext, Expression expression)
        {
            INodeTypeProvider nodeTypeProvider = dbContext.GetService<MethodInfoBasedNodeTypeRegistry>();
            var queryParser = new QueryParser(
                new ExpressionTreeParser(nodeTypeProvider,
                new CompoundExpressionTreeProcessor(
                    new IExpressionTreeProcessor[]
                    {
                            new PartialEvaluatingExpressionTreeProcessor(new ApiCompilationFilter()),
                            new TransformingExpressionTreeProcessor(ExpressionTransformerRegistry.CreateDefault())
                    })));
            return queryParser.GetParsedQuery(expression);
        }
    }
}
