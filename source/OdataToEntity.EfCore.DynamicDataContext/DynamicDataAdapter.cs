using Microsoft.OData.Edm;
using OdataToEntity.ModelBuilder;
using OdataToEntity.Parsers;
using System;
using System.Linq.Expressions;

namespace OdataToEntity.EfCore.DynamicDataContext
{
    public sealed class DynamicDataAdapter : OeEfCoreDataAdapter<DynamicDbContext>
    {
        private readonly Db.OeEntitySetAdapterCollection _dynamicEntitySetAdapters;
        private readonly ExpressionVisitor? _expressionVisitor;

        public DynamicDataAdapter(DynamicTypeDefinitionManager typeDefinitionManager)
            : base(null, null, typeDefinitionManager.OperationAdapter)
        {
            TypeDefinitionManager = typeDefinitionManager;
            _dynamicEntitySetAdapters = CreateEntitySetAdapters(typeDefinitionManager);

            _expressionVisitor = typeDefinitionManager.ExpressionVisitor;
            base.IsDatabaseNullHighestValue = typeDefinitionManager.IsDatabaseNullHighestValue;
            base.IsCaseSensitive = typeDefinitionManager.IsCaseSensitive;
        }

        public EdmModel BuildEdmModel(ModelBuilder.DynamicMetadataProvider metadataProvider)
        {
            using (DynamicDbContext context = TypeDefinitionManager.CreateDynamicDbContext())
            {
                var edmModelMetadataProvider = new ModelBuilder.DynamicEdmModelMetadataProvider(context.Model, metadataProvider, TypeDefinitionManager);
                var modelBuilder = new OeEdmModelBuilder(this, edmModelMetadataProvider);

                foreach (OeOperationConfiguration operationConfiguration in metadataProvider.GetRoutines(TypeDefinitionManager))
                    modelBuilder.AddOperation(operationConfiguration);

                return modelBuilder.BuildEdmModel();
            }
        }
        private static Db.OeEntitySetAdapterCollection CreateEntitySetAdapters(DynamicTypeDefinitionManager typeDefinitionManager)
        {
            var entitySetAdapters = new Db.OeEntitySetAdapter[typeDefinitionManager.TypeDefinitions.Count];
            int i = 0;
            foreach (DynamicTypeDefinition typeDefinition in typeDefinitionManager.TypeDefinitions)
                entitySetAdapters[i++] = CreateEntitySetAdapter(typeDefinition.DynamicTypeType, typeDefinition.TableEdmName, typeDefinition.IsQueryType);
            return new Db.OeEntitySetAdapterCollection(entitySetAdapters);
        }
        public override Object CreateDataContext()
        {
            return TypeDefinitionManager.CreateDynamicDbContext();
        }
        protected override Expression TranslateExpression(IEdmModel edmModel, Expression expression)
        {
            var collectionNavigationVisitor = new OeCollectionNavigationVisitor(edmModel, expression);
            expression = collectionNavigationVisitor.Visit(expression);
            expression = _expressionVisitor == null ? expression : _expressionVisitor.Visit(expression);

            return base.TranslateExpression(edmModel, expression);
        }

        public override Type DataContextType => TypeDefinitionManager.DynamicDbContextType;
        public override Db.OeEntitySetAdapterCollection EntitySetAdapters => _dynamicEntitySetAdapters;
        public DynamicTypeDefinitionManager TypeDefinitionManager { get; }
    }
}
