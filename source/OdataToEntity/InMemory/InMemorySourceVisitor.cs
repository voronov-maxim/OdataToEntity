using Microsoft.OData.Edm;
using OdataToEntity.Parsers;
using System;
using System.Linq.Expressions;

namespace OdataToEntity.InMemory
{
    internal sealed class InMemorySourceVisitor : ExpressionVisitor
    {
        private readonly IEdmModel _edmModel;
        private readonly Object?[] _parameters;

        public InMemorySourceVisitor(IEdmModel edmModel, Object?[] parameters)
        {
            _edmModel = edmModel;
            _parameters = parameters;
        }

        protected override Expression VisitConstant(ConstantExpression node)
        {
            if (node.Value is OeEnumerableStub enumerableStub)
            {
                var entitySetAdapter = (InMemoryEntitySetAdapter)_edmModel.GetEntitySetAdapter(enumerableStub.EntitySet);
                return entitySetAdapter.GetEntitySet(_parameters).Expression;
            }

            return node;
        }
    }
}
