using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Remotion.Linq;
using Remotion.Linq.Parsing.ExpressionVisitors.Transformation;
using Remotion.Linq.Parsing.ExpressionVisitors.TreeEvaluation;
using Remotion.Linq.Parsing.Structure;
using Remotion.Linq.Parsing.Structure.ExpressionTreeProcessors;
using Remotion.Linq.Parsing.Structure.NodeTypeProviders;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace OdataToEntity.EfCore
{
    internal static class EfCoreExtension
    {
        private sealed class ApiCompilationFilter : EvaluatableExpressionFilterBase { }

        public static Func<QueryContext, IAsyncEnumerable<T>> CreateAsyncQueryExecutor<T>(this DbContext dbContext, Expression expression)
        {
            QueryModel queryModel = CreateQueryModel(dbContext, expression);
            var queryCompilationContextFactory = dbContext.GetService<IQueryCompilationContextFactory>();
            return queryCompilationContextFactory.Create(true).CreateQueryModelVisitor().CreateAsyncQueryExecutor<T>(queryModel);
        }
        public static Func<QueryContext, IEnumerable<T>> CreateQueryExecutor<T>(this DbContext dbContext, Expression expression)
        {
            QueryModel queryModel = CreateQueryModel(dbContext, expression);
            var queryCompilationContextFactory = dbContext.GetService<IQueryCompilationContextFactory>();
            return queryCompilationContextFactory.Create(false).CreateQueryModelVisitor().CreateQueryExecutor<T>(queryModel);
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
