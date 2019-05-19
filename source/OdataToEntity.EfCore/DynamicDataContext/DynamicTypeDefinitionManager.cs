using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.OData.Edm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace OdataToEntity.EfCore.DynamicDataContext
{
    public sealed class DynamicTypeDefinitionManager
    {
        private readonly Dictionary<Type, DynamicTypeDefinition> _dynamicTypeDefinitions;
        private static int _dynamicTypeIndex;
        private readonly DbContextOptions _options;
        private readonly Dictionary<String, Type> _tableNameTypes;

        private DynamicTypeDefinitionManager(DbContextOptions options, DynamicMetadataProvider metadataProvider)
        {
            _options = options;
            MetadataProvider = metadataProvider;

            _dynamicTypeDefinitions = new Dictionary<Type, DynamicTypeDefinition>();
            _tableNameTypes = new Dictionary<String, Type>();
        }

        public static DynamicTypeDefinitionManager Create(DbContextOptions options, DynamicMetadataProvider metadataProvider)
        {
            var typeDefinitionManager = new DynamicTypeDefinitionManager(options, metadataProvider);
            var dbContext = new DynamicDbContext(options, new DynamicModelBuilder(typeDefinitionManager));
            _ = dbContext.Model; //force OnModelCreating
            return typeDefinitionManager;
        }
        public DynamicDbContext CreateDynamicDbContext()
        {
            return new DynamicDbContext(_options, this);
        }
        public IQueryable<DynamicType> GetQueryable(DynamicDbContext dynamicDbContext, String tableName)
        {
            return GetQueryable(dynamicDbContext, _tableNameTypes[tableName]);
        }
        public static IQueryable<DynamicType> GetQueryable(DynamicDbContext dynamicDbContext, Type dynamicTypeType)
        {
            Type dbSetType = typeof(InternalDbSet<>).MakeGenericType(new Type[] { dynamicTypeType });
            ConstructorInfo ctor = dbSetType.GetConstructor(new Type[] { typeof(DbContext) });
            return (IQueryable<DynamicType>)ctor.Invoke(new Object[] { dynamicDbContext });
        }
        public DynamicTypeDefinition GetDynamicTypeDefinition(String tableName)
        {
            if (_tableNameTypes.TryGetValue(tableName, out Type dynamicTypeType))
                return GetDynamicTypeDefinition(dynamicTypeType);

            dynamicTypeType = GetDynamicTypeType();
            String entityName = MetadataProvider.GetEntityName(tableName);
            var dynamicTypeDefinition = new DynamicTypeDefinition(dynamicTypeType, entityName, tableName);
            _tableNameTypes.Add(tableName, dynamicTypeType);
            _dynamicTypeDefinitions.Add(dynamicTypeType, dynamicTypeDefinition);
            return dynamicTypeDefinition;
        }
        public DynamicTypeDefinition GetDynamicTypeDefinition(Type dynamicTypeType)
        {
            return _dynamicTypeDefinitions[dynamicTypeType];
        }
        private static Type GetDynamicTypeType()
        {
            int dynamicTypeIndex = Interlocked.Increment(ref _dynamicTypeIndex);
            return Type.GetType(typeof(DynamicType).FullName + dynamicTypeIndex.ToString("D2"));
        }

        public DynamicMetadataProvider MetadataProvider { get; }
        public ICollection<DynamicTypeDefinition> TypeDefinitions => _dynamicTypeDefinitions.Values;
    }
}
