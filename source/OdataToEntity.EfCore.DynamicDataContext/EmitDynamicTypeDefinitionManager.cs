using OdataToEntity.EfCore.DynamicDataContext.InformationSchema;
using OdataToEntity.EfCore.DynamicDataContext.ModelBuilder;
using System;
using System.Reflection;
using System.Reflection.Emit;

namespace OdataToEntity.EfCore.DynamicDataContext
{
    public class EmitDynamicTypeDefinitionManagerFactory : DynamicTypeDefinitionManagerFactory
    {
        public override DynamicTypeDefinitionManager Create(DynamicMetadataProvider metadataProvider)
        {
            return EmitDynamicTypeDefinitionManager.Create(metadataProvider, CreateDynamicDbContextType());
        }
    }

    public sealed class EmitDynamicTypeDefinitionManager : DynamicTypeDefinitionManager
    {
        private static readonly ModuleBuilder _moduleBuilder = CreateTypeBuilder();

        private EmitDynamicTypeDefinitionManager(Type dynamicDbContextType, ProviderSpecificSchema informationSchema)
            : base(dynamicDbContextType, informationSchema)
        {
        }

        internal static new DynamicTypeDefinitionManager Create(DynamicMetadataProvider metadataProvider, Type dynamicDbContextType)
        {
            var typeDefinitionManager = new EmitDynamicTypeDefinitionManager(dynamicDbContextType, metadataProvider.InformationSchema);
            InitializeDbContext(metadataProvider, dynamicDbContextType, typeDefinitionManager);
            return typeDefinitionManager;
        }
        private static ModuleBuilder CreateTypeBuilder()
        {
            var assemblyName = new AssemblyName("OdataToEntityDynamicTypes");
            AssemblyBuilder assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.RunAndCollect);
            return assemblyBuilder.DefineDynamicModule("OdataToEntityDynamicTypes");
        }
        internal override DynamicTypeDefinition GetOrAddDynamicTypeDefinition(in TableFullName tableFullName, bool isQueryType, string tableEdmName)
        {
            String fullName = tableFullName.Schema + "." + tableFullName.Name;
            Type? dynamicTypeType = _moduleBuilder.GetType(fullName);
            if (dynamicTypeType == null)
            {
                TypeBuilder typeBuilder = _moduleBuilder.DefineType(fullName, TypeAttributes.Public | TypeAttributes.Sealed, typeof(Parsers.OeDynamicType));
                dynamicTypeType = typeBuilder.CreateType();
                if (dynamicTypeType == null)
                    throw new InvalidProgramException("Cannot create DynamicType " + fullName);
            }
            else
            {
                DynamicTypeDefinition? dynamicTypeDefinition = base.TryGetDynamicTypeDefinition(tableFullName);
                if (dynamicTypeDefinition != null)
                    return dynamicTypeDefinition;
            }

            return base.CreateDynamicTypeDefinition(tableFullName, isQueryType, tableEdmName, dynamicTypeType);
        }
    }
}
