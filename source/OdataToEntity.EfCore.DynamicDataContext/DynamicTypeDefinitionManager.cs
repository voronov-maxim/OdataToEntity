using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Query.Internal;
using OdataToEntity.EfCore.DynamicDataContext.Types;
using System;
using System.Collections.Generic;
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
        private readonly Dictionary<String, Type> _tableNameTypes;

        private DynamicTypeDefinitionManager(DbContextOptions options, DynamicMetadataProvider metadataProvider, ConstructorInfo dynamicDbContextCtor)
        {
            MetadataProvider = metadataProvider;

            NewExpression ctor = Expression.New(dynamicDbContextCtor, Expression.Constant(options), Expression.Constant(this));
            _dynamicDbContextCtor = Expression.Lambda<Func<DynamicDbContext>>(ctor).Compile();

            _dynamicTypeDefinitions = new Dictionary<Type, DynamicTypeDefinition>();
            _tableNameTypes = new Dictionary<String, Type>();
        }

        public static DynamicTypeDefinitionManager Create(DynamicMetadataProvider metadataProvider)
        {
            int dynamicDbContextIndex = Interlocked.Increment(ref _dynamicDbContextIndex);
            Type dynamicDbContextType = Type.GetType(typeof(DynamicDbContext).FullName + dynamicDbContextIndex.ToString("D2"));
            if (dynamicDbContextType == null)
                throw new InvalidOperationException("DynamicDbContext out range " + dynamicDbContextIndex.ToString());

            ConstructorInfo ctor = dynamicDbContextType.GetConstructor(new Type[] { typeof(DbContextOptions), typeof(DynamicTypeDefinitionManager) });
            DbContextOptions options = CreateOptions(metadataProvider.DynamicDbContextOptions, dynamicDbContextType);
            var typeDefinitionManager = new DynamicTypeDefinitionManager(options, metadataProvider, ctor);

            ctor = dynamicDbContextType.GetConstructor(new Type[] { typeof(DbContextOptions), typeof(DynamicModelBuilder).MakeByRefType() });
            var dbContext = (DynamicDbContext)ctor.Invoke(new Object[] { options, new DynamicModelBuilder(typeDefinitionManager) });
            _ = dbContext.Model; //force OnModelCreating
            return typeDefinitionManager;
        }
        public DynamicDbContext CreateDynamicDbContext()
        {
            return _dynamicDbContextCtor();
        }
        private static DbContextOptions CreateOptions(DbContextOptions options, Type dynamicDbContextType)
        {
            Type optionsBuilderType = typeof(DbContextOptionsBuilder<>).MakeGenericType(dynamicDbContextType);
            var optionsBuilder = (DbContextOptionsBuilder)Activator.CreateInstance(optionsBuilderType);
            DbContextOptions dynamicContextOptions = optionsBuilder.Options;
            foreach (IDbContextOptionsExtension extension in options.Extensions)
                dynamicContextOptions = dynamicContextOptions.WithExtension(extension);
            return dynamicContextOptions;
        }
        public IQueryable<DynamicType> GetQueryable(DynamicDbContext dynamicDbContext, String tableName)
        {
            return GetQueryable(dynamicDbContext, _tableNameTypes[tableName]);
        }
        public static IQueryable<DynamicType> GetQueryable(DynamicDbContext dynamicDbContext, Type dynamicTypeType)
        {
            Type dbSetType = typeof(EntityQueryable<>).MakeGenericType(new Type[] { dynamicTypeType });
            ConstructorInfo ctor = dbSetType.GetConstructor(new Type[] { typeof(IAsyncQueryProvider) });
            return (IQueryable<DynamicType>)ctor.Invoke(new Object[] { dynamicDbContext.GetDependencies().QueryProvider });
        }
        public DynamicTypeDefinition GetDynamicTypeDefinition(String tableName, bool isQueryType)
        {
            if (_tableNameTypes.TryGetValue(tableName, out Type dynamicTypeType))
                return GetDynamicTypeDefinition(dynamicTypeType);

            dynamicTypeType = GetDynamicTypeType();
            String entityName = MetadataProvider.GetEntityName(tableName);
            var dynamicTypeDefinition = new DynamicTypeDefinition(dynamicTypeType, entityName, tableName, isQueryType);
            _tableNameTypes.Add(tableName, dynamicTypeType);
            _dynamicTypeDefinitions.Add(dynamicTypeType, dynamicTypeDefinition);
            return dynamicTypeDefinition;
        }
        public DynamicTypeDefinition GetDynamicTypeDefinition(Type dynamicTypeType)
        {
            return _dynamicTypeDefinitions[dynamicTypeType];
        }
        private Type GetDynamicTypeType()
        {
            _dynamicTypeIndex++;
            return Type.GetType(typeof(DynamicType).FullName + _dynamicTypeIndex.ToString("D2"));
        }

        public DynamicMetadataProvider MetadataProvider { get; }
        public ICollection<DynamicTypeDefinition> TypeDefinitions => _dynamicTypeDefinitions.Values;
    }
}
