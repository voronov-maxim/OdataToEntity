using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using System;
using System.Reflection;

namespace OdataToEntity.EfCore.Fix
{
    internal sealed class FixRelationalSqlTranslatingExpressionVisitorFactory<TOriginalFactory>
        : IRelationalSqlTranslatingExpressionVisitorFactory where TOriginalFactory : IRelationalSqlTranslatingExpressionVisitorFactory
    {
        private readonly RelationalSqlTranslatingExpressionVisitorDependencies _dependencies;
        private readonly IRelationalSqlTranslatingExpressionVisitorFactory _originalFactory;

        public FixRelationalSqlTranslatingExpressionVisitorFactory(RelationalSqlTranslatingExpressionVisitorDependencies dependencies)
        {
            ConstructorInfo ctor = typeof(TOriginalFactory).GetConstructor(new[] { typeof(RelationalSqlTranslatingExpressionVisitorDependencies) });
            _originalFactory = (IRelationalSqlTranslatingExpressionVisitorFactory)ctor.Invoke(new Object[] { dependencies });
            _dependencies = dependencies;
        }

        public RelationalSqlTranslatingExpressionVisitor Create(IModel model, QueryableMethodTranslatingExpressionVisitor queryableMethodTranslatingExpressionVisitor)
        {
            RelationalSqlTranslatingExpressionVisitor originalVisitor = _originalFactory.Create(model, queryableMethodTranslatingExpressionVisitor);
            return new FixSqlServerSqlTranslatingExpressionVisitor(_dependencies, model, queryableMethodTranslatingExpressionVisitor);
        }
    }
}
