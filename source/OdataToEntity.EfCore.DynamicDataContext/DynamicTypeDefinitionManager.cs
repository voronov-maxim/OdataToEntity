using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;
using OdataToEntity.EfCore.DynamicDataContext.InformationSchema;
using OdataToEntity.EfCore.DynamicDataContext.ModelBuilder;
using OdataToEntity.EfCore.DynamicDataContext.Types;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;

namespace OdataToEntity.EfCore.DynamicDataContext
{
    public sealed class DynamicTypeDefinitionManager
    {
        private readonly Dictionary<Type, DynamicTypeDefinition> _dynamicTypeDefinitions;
        private readonly Func<DynamicDbContext> _dynamicDbContextCtor;
        private static int _dynamicDbContextIndex;
        private int _dynamicTypeIndex;
        private readonly Dictionary<TableFullName, Type> _tableEdmNameTypes;

        private DynamicTypeDefinitionManager(Type dynamicDbContextType, ProviderSpecificSchema informationSchema)
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

            _dynamicTypeDefinitions = new Dictionary<Type, DynamicTypeDefinition>();
            _tableEdmNameTypes = new Dictionary<TableFullName, Type>(informationSchema.IsCaseSensitive ? TableFullName.OrdinalComparer : TableFullName.OrdinalIgnoreCaseComparer);
        }

        public static DynamicTypeDefinitionManager Create(DynamicMetadataProvider metadataProvider)
        {
            int dynamicDbContextIndex = Interlocked.Increment(ref _dynamicDbContextIndex);
            String fullName = typeof(DynamicDbContext1).Namespace + "." + nameof(DynamicDbContext) + dynamicDbContextIndex.ToString(CultureInfo.InvariantCulture);
            Type? dynamicDbContextType = Type.GetType(fullName);
            if (dynamicDbContextType == null)
                throw new InvalidOperationException("DynamicDbContext out range " + dynamicDbContextIndex.ToString(CultureInfo.InvariantCulture));

            var typeDefinitionManager = new DynamicTypeDefinitionManager(dynamicDbContextType, metadataProvider.InformationSchema);

            ConstructorInfo ctor = dynamicDbContextType.GetConstructor(new Type[] { typeof(DbContextOptions), typeof(DynamicModelBuilder).MakeByRefType() })!;
            DbContextOptions options = metadataProvider.InformationSchema.DynamicDbContextOptions.CreateOptions(dynamicDbContextType);
            var dbContext = (DynamicDbContext)ctor.Invoke(new Object[] { options, new DynamicModelBuilder(metadataProvider, typeDefinitionManager) });
            _ = dbContext.Model; //force OnModelCreating
            return typeDefinitionManager;
        }
        public DynamicDbContext CreateDynamicDbContext()
        {
            return _dynamicDbContextCtor();
        }
        public DynamicTypeDefinition GetDynamicTypeDefinition(Type dynamicTypeType)
        {
            return _dynamicTypeDefinitions[dynamicTypeType];
        }
        public DynamicTypeDefinition GetDynamicTypeDefinition(in TableFullName tableFullName)
        {
            return GetDynamicTypeDefinition(_tableEdmNameTypes[tableFullName]);
        }
        internal DynamicTypeDefinition GetOrAddDynamicTypeDefinition(in TableFullName tableFullName, bool isQueryType, String tableEdmName)
        {
            if (_tableEdmNameTypes.TryGetValue(tableFullName, out Type? dynamicTypeType))
                return GetDynamicTypeDefinition(dynamicTypeType);

            _dynamicTypeIndex++;
            dynamicTypeType = Type.GetType("OdataToEntity.EfCore.DynamicDataContext.Types.DynamicType" + _dynamicTypeIndex.ToString(CultureInfo.InvariantCulture));
            if (dynamicTypeType == null)
                throw new InvalidProgramException("Cannot create DynamicType index " + _dynamicTypeIndex.ToString(CultureInfo.InvariantCulture) + " out of range");

            var dynamicTypeDefinition = new DynamicTypeDefinition(dynamicTypeType, tableFullName, isQueryType, tableEdmName);
            _tableEdmNameTypes.Add(tableFullName, dynamicTypeType);
            _dynamicTypeDefinitions.Add(dynamicTypeType, dynamicTypeDefinition);
            return dynamicTypeDefinition;
        }

        public Type DynamicDbContextType { get; }
        public ExpressionVisitor? ExpressionVisitor { get; }
        public bool IsCaseSensitive { get; }
        public bool IsDatabaseNullHighestValue { get; }
        public OeEfCoreOperationAdapter OperationAdapter { get; }
        public ICollection<DynamicTypeDefinition> TypeDefinitions => _dynamicTypeDefinitions.Values;
    }
}
