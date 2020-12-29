using Microsoft.EntityFrameworkCore;
using OdataToEntity.EfCore.DynamicDataContext.InformationSchema;
using OdataToEntity.EfCore.DynamicDataContext.ModelBuilder;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

namespace OdataToEntity.EfCore.DynamicDataContext
{
    public class DynamicTypeDefinitionManager
    {
        private readonly Func<DynamicDbContext> _dynamicDbContextCtor;
        private int _dynamicTypeIndex;
        private readonly Dictionary<Type, DynamicTypeDefinition> _dynamicTypeType2DynamicTypeDefinitions;
        private readonly Dictionary<TableFullName, DynamicTypeDefinition> _tableFullName2DynamicTypeDefinitions;

        protected DynamicTypeDefinitionManager(Type dynamicDbContextType, ProviderSpecificSchema informationSchema)
        {
            DynamicDbContextType = dynamicDbContextType;

            ExpressionVisitor = informationSchema.ExpressionVisitor;
            IsCaseSensitive = informationSchema.IsCaseSensitive;
            IsDatabaseNullHighestValue = informationSchema.IsDatabaseNullHighestValue;
            OperationAdapter = informationSchema.OperationAdapter;

            ConstructorInfo dynamicDbContextCtor = dynamicDbContextType.GetConstructor(new Type[] { typeof(DbContextOptions) })!;
            DbContextOptions options = informationSchema.DynamicDbContextOptions.CreateOptions(dynamicDbContextType);
            NewExpression ctor = Expression.New(dynamicDbContextCtor, Expression.Constant(options));
            _dynamicDbContextCtor = Expression.Lambda<Func<DynamicDbContext>>(ctor).Compile();

            _dynamicTypeType2DynamicTypeDefinitions = new Dictionary<Type, DynamicTypeDefinition>();
            _tableFullName2DynamicTypeDefinitions = new Dictionary<TableFullName, DynamicTypeDefinition>();
        }

        internal static DynamicTypeDefinitionManager Create(DynamicMetadataProvider metadataProvider, Type dynamicDbContextType)
        {
            var typeDefinitionManager = new DynamicTypeDefinitionManager(dynamicDbContextType, metadataProvider.InformationSchema);
            InitializeDbContext(metadataProvider, dynamicDbContextType, typeDefinitionManager);
            return typeDefinitionManager;
        }
        public DynamicDbContext CreateDynamicDbContext()
        {
            return _dynamicDbContextCtor();
        }
        protected DynamicTypeDefinition CreateDynamicTypeDefinition(in TableFullName tableFullName, bool isQueryType, String tableEdmName, Type dynamicTypeType)
        {
            var dynamicTypeDefinition = new DynamicTypeDefinition(dynamicTypeType, tableFullName, isQueryType, tableEdmName);
            _tableFullName2DynamicTypeDefinitions.Add(tableFullName, dynamicTypeDefinition);
            _dynamicTypeType2DynamicTypeDefinitions.Add(dynamicTypeType, dynamicTypeDefinition);
            return dynamicTypeDefinition;
        }
        public DynamicTypeDefinition GetDynamicTypeDefinition(Type dynamicTypeType)
        {
            return _dynamicTypeType2DynamicTypeDefinitions[dynamicTypeType];
        }
        public DynamicTypeDefinition GetDynamicTypeDefinition(in TableFullName tableFullName)
        {
            return _tableFullName2DynamicTypeDefinitions[tableFullName];
        }
        internal virtual DynamicTypeDefinition GetOrAddDynamicTypeDefinition(in TableFullName tableFullName, bool isQueryType, String tableEdmName)
        {
            DynamicTypeDefinition? dynamicTypeDefinition = TryGetDynamicTypeDefinition(tableFullName);
            if (dynamicTypeDefinition != null)
                return dynamicTypeDefinition;

            _dynamicTypeIndex++;
            Type? dynamicTypeType = Type.GetType("OdataToEntity.EfCore.DynamicDataContext.Types.DynamicType" + _dynamicTypeIndex.ToString(CultureInfo.InvariantCulture));
            if (dynamicTypeType == null)
                throw new InvalidProgramException("Cannot create DynamicType index " + _dynamicTypeIndex.ToString(CultureInfo.InvariantCulture) + " out of range");

            return CreateDynamicTypeDefinition(tableFullName, isQueryType, tableEdmName, dynamicTypeType);
        }
        protected static void InitializeDbContext(DynamicMetadataProvider metadataProvider, Type dynamicDbContextType, DynamicTypeDefinitionManager typeDefinitionManager)
        {
            ConstructorInfo ctor = dynamicDbContextType.GetConstructor(new Type[] { typeof(DbContextOptions), typeof(DynamicModelBuilder).MakeByRefType() })!;
            DbContextOptions options = metadataProvider.InformationSchema.DynamicDbContextOptions.CreateOptions(dynamicDbContextType);
            var dbContext = (DynamicDbContext)ctor.Invoke(new Object[] { options, new DynamicModelBuilder(metadataProvider, typeDefinitionManager) });
            _ = dbContext.Model; //force OnModelCreating
        }
        protected DynamicTypeDefinition? TryGetDynamicTypeDefinition(in TableFullName tableFullName)
        {
            _tableFullName2DynamicTypeDefinitions.TryGetValue(tableFullName, out DynamicTypeDefinition? dynamicTypeDefinition);
            return dynamicTypeDefinition;
        }

        public Type DynamicDbContextType { get; }
        public ExpressionVisitor? ExpressionVisitor { get; }
        public bool IsCaseSensitive { get; }
        public bool IsDatabaseNullHighestValue { get; }
        public OeEfCoreOperationAdapter OperationAdapter { get; }
        public ICollection<DynamicTypeDefinition> TypeDefinitions => _tableFullName2DynamicTypeDefinitions.Values;
    }
}
