using Microsoft.OData.Edm;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace OdataToEntity.ModelBuilder
{
    public sealed class OeEdmModelBuilder
    {
        private readonly Dictionary<Type, EdmComplexType> _complexTypes;
        private readonly Db.OeDataAdapter _dataAdapter;
        private readonly Dictionary<Type, EdmEnumType> _enumTypes;
        private readonly OeEdmModelMetadataProvider _metadataProvider;
        private readonly List<OeOperationConfiguration> _operationConfigurations;

        public OeEdmModelBuilder(Db.OeDataAdapter dataAdapter, OeEdmModelMetadataProvider metadataProvider)
        {
            _dataAdapter = dataAdapter;
            _metadataProvider = metadataProvider;

            _complexTypes = new Dictionary<Type, EdmComplexType>();
            _enumTypes = new Dictionary<Type, EdmEnumType>();
            _operationConfigurations = new List<OeOperationConfiguration>();
        }

        public void AddComplexType(Type complexType)
        {
            var edmComplexType = new EdmComplexType(complexType.Namespace, complexType.Name);
            _complexTypes.Add(complexType, edmComplexType);
        }
        public void AddEnumType(Type enumType)
        {
            _enumTypes.Add(enumType, CreateEdmEnumType(enumType));
        }
        public void AddOperation(OeOperationConfiguration operationConfiguration)
        {
            _operationConfigurations.Add(operationConfiguration);
        }
        private void AddOperations()
        {
            IReadOnlyList<OeOperationConfiguration> operations = _dataAdapter.OperationAdapter.GetOperations();
            if (operations != null)
                for (int i = 0; i < operations.Count; i++)
                    AddOperation(operations[i]);
        }
        private EdmAction BuildAction(OeOperationConfiguration operationConfiguration, Dictionary<Type, EntityTypeInfo> entityTypeInfos)
        {
            var edmAction = new EdmAction(operationConfiguration.NamespaceName, operationConfiguration.Name, null);
            foreach (OeOperationParameterConfiguration parameterConfiguration in operationConfiguration.Parameters)
            {
                IEdmTypeReference edmTypeReference = GetEdmTypeReference(parameterConfiguration.ClrType, entityTypeInfos);
                edmAction.AddParameter(parameterConfiguration.Name, edmTypeReference);
            }

            return edmAction;
        }
        public EdmModel BuildEdmModel(params IEdmModel[] refModels)
        {
            AddOperations();
            var edmModel = new EdmModel(false);

            Dictionary<Type, EntityTypeInfo> entityTypeInfos = BuildEntityTypes(refModels);
            foreach (EntityTypeInfo typeInfo in entityTypeInfos.Values)
                if (!typeInfo.IsRefModel)
                    typeInfo.BuildStructuralProperties(edmModel, entityTypeInfos, _enumTypes, _complexTypes);

            foreach (EntityTypeInfo typeInfo in entityTypeInfos.Values)
                if (!typeInfo.IsRefModel)
                    foreach (FKeyInfo fkeyInfo in typeInfo.NavigationClrProperties)
                        fkeyInfo.BuildNavigationProperty();

            edmModel.AddElements(_enumTypes.Values);
            foreach (KeyValuePair<Type, EdmEnumType> enumType in _enumTypes)
                edmModel.SetClrType(enumType.Value, enumType.Key);

            edmModel.AddElements(_complexTypes.Values);
            foreach (KeyValuePair<Type, EdmComplexType> complexType in _complexTypes)
                edmModel.SetClrType(complexType.Value, complexType.Key);

            var container = new EdmEntityContainer(_dataAdapter.DataContextType.Namespace, _dataAdapter.DataContextType.Name);
            edmModel.SetDataAdapter(container, _dataAdapter);

            var entitySets = new Dictionary<IEdmEntityType, EdmEntitySet>(entityTypeInfos.Count);
            foreach (EntityTypeInfo typeInfo in entityTypeInfos.Values)
            {
                if (!typeInfo.IsRefModel)
                {
                    edmModel.AddElement(typeInfo.EdmType);
                    edmModel.SetClrType(typeInfo.EdmType, typeInfo.ClrType);
                }

                Db.OeEntitySetAdapter? entitySetAdapter = _dataAdapter.EntitySetAdapters.Find(typeInfo.ClrType);
                if (entitySetAdapter != null)
                {
                    EdmEntitySet entitySet = container.AddEntitySet(entitySetAdapter.EntitySetName, typeInfo.EdmType);
                    edmModel.SetEntitySetAdapter(entitySet, entitySetAdapter);
                    entitySets.Add(typeInfo.EdmType, entitySet);
                }
            }

            var manyToManyBuilder = new ManyToManyBuilder(edmModel, _metadataProvider, entityTypeInfos);
            foreach (EntityTypeInfo typeInfo in entityTypeInfos.Values)
                if (!typeInfo.IsRefModel)
                {
                    foreach (FKeyInfo fkeyInfo in typeInfo.NavigationClrProperties)
                    {
                        EdmEntitySet principal = entitySets[fkeyInfo.PrincipalInfo.EdmType];
                        EdmEntitySet dependent = entitySets[fkeyInfo.DependentInfo.EdmType];

                        if (fkeyInfo.DependentNavigationProperty == null)
                            principal.AddNavigationTarget(fkeyInfo.EdmNavigationProperty, dependent);
                        else
                        {
                            dependent.AddNavigationTarget(fkeyInfo.EdmNavigationProperty, principal);
                            if (fkeyInfo.EdmNavigationProperty.Partner != null)
                                principal.AddNavigationTarget(fkeyInfo.EdmNavigationProperty.Partner, dependent);
                        }
                    }

                    manyToManyBuilder.Build(typeInfo);
                }

            foreach (OeOperationConfiguration operationConfiguration in _operationConfigurations)
            {
                if (operationConfiguration.ReturnType == typeof(void))
                {
                    EdmAction edmAction = BuildAction(operationConfiguration, entityTypeInfos);
                    edmModel.AddElement(edmAction);
                    container.AddActionImport(operationConfiguration.ImportName, edmAction);
                    edmModel.SetIsDbFunction(edmAction, operationConfiguration.IsDbFunction);
                }
                else
                {
                    EdmFunction edmFunction = BuildFunction(operationConfiguration, entityTypeInfos);
                    edmModel.AddElement(edmFunction);

                    if (edmFunction.IsBound)
                    {
                        if (operationConfiguration.MethodInfo == null)
                            throw new InvalidOperationException("Bound function operationConfiguration.MethodInfo must be not null");

                        edmModel.SetMethodInfo(edmFunction, operationConfiguration.MethodInfo);
                    }
                    else
                    {
                        container.AddFunctionImport(operationConfiguration.ImportName, edmFunction, edmFunction.EntitySetPath);
                        edmModel.SetIsDbFunction(edmFunction, operationConfiguration.IsDbFunction);
                    }
                }
            }

            edmModel.AddElement(container);
            _dataAdapter.SetEdmModel(edmModel);
            foreach (IEdmModel refModel in refModels)
                edmModel.AddReferencedModel(refModel);

            return edmModel;
        }
        private Dictionary<Type, EntityTypeInfo> BuildEntityTypes(IEdmModel[] refModels)
        {
            var entityTypeInfos = new Dictionary<Type, EntityTypeInfo>();
            foreach (Db.OeEntitySetAdapter entitySetAdapter in _dataAdapter.EntitySetAdapters)
            {
                var baseClrTypes = new Stack<Type>();
                Type clrType = entitySetAdapter.EntityType;
                do
                {
                    baseClrTypes.Push(clrType);
                    clrType = clrType.BaseType!;
                }
                while (!(clrType == typeof(Object) || clrType == typeof(ValueType)));

                EdmEntityType? edmType = null;
                foreach (Type baseClrType in baseClrTypes)
                    if (entityTypeInfos.TryGetValue(baseClrType, out EntityTypeInfo? entityTypeInfo))
                        edmType = entityTypeInfo.EdmType;
                    else
                    {
                        EdmEntityType? baseEdmType = GetBaseEdmEntityType(refModels, baseClrType);
                        if (baseEdmType == null)
                        {
                            edmType = new EdmEntityType(baseClrType.Namespace, baseClrType.Name, edmType, baseClrType.IsAbstract, false);
                            entityTypeInfo = new EntityTypeInfo(_metadataProvider, baseClrType, edmType, false, entitySetAdapter.IsDbQuery);
                        }
                        else
                        {
                            edmType = baseEdmType;
                            entityTypeInfo = new EntityTypeInfo(_metadataProvider, baseClrType, edmType, true, entitySetAdapter.IsDbQuery);
                        }
                        entityTypeInfos.Add(baseClrType, entityTypeInfo);
                    }
            }
            return entityTypeInfos;

            static EdmEntityType? GetBaseEdmEntityType(IEdmModel[] refModels, Type clrType)
            {
                foreach (IEdmModel refModel in refModels)
                    foreach (IEdmSchemaElement element in refModel.SchemaElements)
                        if (element is EdmEntityType edmEntityType &&
                            String.Compare(edmEntityType.Name, clrType.Name, StringComparison.OrdinalIgnoreCase) == 0 &&
                            String.Compare(edmEntityType.Namespace, clrType.Namespace, StringComparison.OrdinalIgnoreCase) == 0)
                            return edmEntityType;

                return null;
            }
        }
        private EdmFunction BuildFunction(OeOperationConfiguration operationConfiguration, Dictionary<Type, EntityTypeInfo> entityTypeInfos)
        {
            IEdmTypeReference edmTypeReference;
            Type? itemType = Parsers.OeExpressionHelper.GetCollectionItemTypeOrNull(operationConfiguration.ReturnType);
            if (itemType == null)
                edmTypeReference = GetEdmTypeReference(operationConfiguration.ReturnType, entityTypeInfos);
            else
                edmTypeReference = new EdmCollectionTypeReference(new EdmCollectionType(GetEdmTypeReference(itemType, entityTypeInfos)));

            var edmFunction = new EdmFunction(operationConfiguration.NamespaceName, operationConfiguration.Name,
                edmTypeReference, operationConfiguration.IsBound, null, false);
            foreach (OeOperationParameterConfiguration parameterConfiguration in operationConfiguration.Parameters)
            {
                edmTypeReference = GetEdmTypeReference(parameterConfiguration.ClrType, entityTypeInfos);
                edmFunction.AddParameter(parameterConfiguration.Name, edmTypeReference);
            }

            return edmFunction;
        }
        public static EdmEnumType CreateEdmEnumType(Type clrEnumType)
        {
            var edmEnumType = new EdmEnumType(clrEnumType.Namespace, clrEnumType.Name);
            foreach (Enum clrMember in Enum.GetValues(clrEnumType))
            {
                long value = Convert.ToInt64(clrMember, CultureInfo.InvariantCulture);
                var edmMember = new EdmEnumMember(edmEnumType, clrMember.ToString(), new EdmEnumMemberValue(value));
                edmEnumType.AddMember(edmMember);
            }
            return edmEnumType;
        }
        private IEdmTypeReference GetEdmTypeReference(Type clrType, Dictionary<Type, EntityTypeInfo> entityTypeInfos)
        {
            bool nullable = PrimitiveTypeHelper.IsNullable(clrType);
            if (nullable)
            {
                Type? underlyingType = Nullable.GetUnderlyingType(clrType);
                if (underlyingType != null)
                    clrType = underlyingType;
            }

            if (entityTypeInfos.TryGetValue(clrType, out EntityTypeInfo? entityTypeInfo))
                return new EdmEntityTypeReference(entityTypeInfo.EdmType, nullable);

            if (_enumTypes.TryGetValue(clrType, out EdmEnumType? edmEnumType))
                return new EdmEnumTypeReference(edmEnumType, nullable);

            if (_complexTypes.TryGetValue(clrType, out EdmComplexType? edmComplexType))
                return new EdmComplexTypeReference(edmComplexType, nullable);

            IEdmTypeReference? typeRef = PrimitiveTypeHelper.GetPrimitiveTypeRef(clrType, nullable);
            if (typeRef != null)
                return typeRef;

            Type itemType = Parsers.OeExpressionHelper.GetCollectionItemType(clrType);
            typeRef = GetEdmTypeReference(itemType, entityTypeInfos);
            return new EdmCollectionTypeReference(new EdmCollectionType(typeRef));
        }
    }
}
