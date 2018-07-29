using Microsoft.OData.Edm;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace OdataToEntity.ModelBuilder
{
    public sealed class OeEdmModelBuilder
    {
        private readonly Dictionary<Type, EdmComplexType> _complexTypes;
        private readonly Dictionary<String, Type> _entitySets;
        private readonly Dictionary<Type, EdmEnumType> _enumTypes;
        private readonly OeEdmModelMetadataProvider _metadataProvider;
        private readonly List<OeOperationConfiguration> _operationConfigurations;

        public OeEdmModelBuilder(OeEdmModelMetadataProvider metadataProvider)
        {
            _metadataProvider = metadataProvider;

            _entitySets = new Dictionary<String, Type>();
            _complexTypes = new Dictionary<Type, EdmComplexType>();
            _enumTypes = new Dictionary<Type, EdmEnumType>();
            _operationConfigurations = new List<OeOperationConfiguration>();
        }

        public void AddComplexType(Type complexType)
        {
            var edmComplexType = new EdmComplexType(complexType.Namespace, complexType.Name);
            _complexTypes.Add(complexType, edmComplexType);
        }
        public void AddEntitySet(String entitySetName, Type entitySetType)
        {
            _entitySets.Add(entitySetName, entitySetType);
        }
        public void AddEntitySetRange(IEnumerable<KeyValuePair<String, Type>> entitySets)
        {
            foreach (KeyValuePair<String, Type> entitySet in entitySets)
                _entitySets.Add(entitySet.Key, entitySet.Value);
        }
        public void AddEnumType(Type enumType)
        {
            _enumTypes.Add(enumType, EntityTypeInfo.CreateEdmEnumType(enumType));
        }
        public void AddOperation(OeOperationConfiguration operationConfiguration)
        {
            _operationConfigurations.Add(operationConfiguration);
        }
        private EdmAction BuildAction(OeOperationConfiguration operationConfiguration, Dictionary<Type, EntityTypeInfo> entityTypeInfos)
        {
            var edmAction = new EdmAction(operationConfiguration.NamespaceName, operationConfiguration.MethodInfoName, null);
            foreach (OeOperationParameterConfiguration parameterConfiguration in operationConfiguration.Parameters)
            {
                IEdmTypeReference edmTypeReference = GetEdmTypeReference(parameterConfiguration.ClrType, entityTypeInfos);
                if (edmTypeReference == null)
                    return null;

                edmAction.AddParameter(parameterConfiguration.Name, edmTypeReference);
            }

            return edmAction;
        }
        public EdmModel BuildEdmModel()
        {
            Dictionary<Type, EntityTypeInfo> entityTypeInfos = BuildEntityTypes();

            foreach (EntityTypeInfo typeInfo in entityTypeInfos.Values)
                typeInfo.BuildProperties(entityTypeInfos, _enumTypes, _complexTypes);

            foreach (EntityTypeInfo typeInfo in entityTypeInfos.Values)
                foreach (FKeyInfo fkeyInfo in typeInfo.NavigationClrProperties)
                    fkeyInfo.EdmNavigationProperty = CreateNavigationProperty(fkeyInfo);

            var edmModel = new EdmModel();
            var container = new EdmEntityContainer("Default", "Container");

            edmModel.AddElements(_enumTypes.Values);
            foreach (KeyValuePair<Type, EdmEnumType> enumType in _enumTypes)
                edmModel.SetClrType(enumType.Value, enumType.Key);

            edmModel.AddElements(_complexTypes.Values);
            foreach (KeyValuePair<Type, EdmComplexType> complexType in _complexTypes)
                edmModel.SetClrType(complexType.Value, complexType.Key);

            var entitySets = new Dictionary<IEdmEntityType, EdmEntitySet>(entityTypeInfos.Count);
            foreach (EntityTypeInfo typeInfo in entityTypeInfos.Values)
            {
                edmModel.AddElement(typeInfo.EdmType);
                edmModel.SetClrType(typeInfo.EdmType, typeInfo.ClrType);

                foreach (KeyValuePair<String, Type> pair in _entitySets)
                    if (pair.Value == typeInfo.ClrType)
                    {
                        entitySets.Add(typeInfo.EdmType, container.AddEntitySet(pair.Key, typeInfo.EdmType));
                        break;
                    }
            }

            foreach (EntityTypeInfo typeInfo in entityTypeInfos.Values)
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

            foreach (OeOperationConfiguration operationConfiguration in _operationConfigurations)
            {
                if (operationConfiguration.IsEdmFunction)
                {
                    EdmFunction edmFunction = BuildFunction(operationConfiguration, entityTypeInfos);
                    if (edmFunction != null)
                    {
                        edmModel.AddElement(edmFunction);
                        container.AddFunctionImport(operationConfiguration.Name, edmFunction);
                        edmModel.SetIsDbFunction(edmFunction, operationConfiguration.IsDbFunction);
                    }
                }
                else
                {
                    EdmAction edmAction = BuildAction(operationConfiguration, entityTypeInfos);
                    if (edmAction != null)
                    {
                        edmModel.AddElement(edmAction);
                        container.AddActionImport(operationConfiguration.Name, edmAction);
                        edmModel.SetIsDbFunction(edmAction, operationConfiguration.IsDbFunction);
                    }
                }
            }

            edmModel.AddElement(container);
            return edmModel;
        }
        private Dictionary<Type, EntityTypeInfo> BuildEntityTypes()
        {
            var entityTypeInfos = new Dictionary<Type, EntityTypeInfo>();
            foreach (Type entitySetClrType in _entitySets.Values)
            {
                var baseClrTypes = new Stack<Type>(1);
                Type clrType = entitySetClrType;
                do
                {
                    baseClrTypes.Push(clrType);
                    clrType = clrType.BaseType;
                }
                while (clrType != typeof(Object));

                EdmEntityType edmType = null;
                foreach (Type baseClrType in baseClrTypes)
                    if (entityTypeInfos.TryGetValue(baseClrType, out EntityTypeInfo entityTypeInfo))
                        edmType = entityTypeInfo.EdmType;
                    else
                    {
                        edmType = new EdmEntityType(baseClrType.Namespace, baseClrType.Name, edmType, baseClrType.IsAbstract, false);
                        entityTypeInfo = new EntityTypeInfo(_metadataProvider, baseClrType, edmType);
                        entityTypeInfos.Add(baseClrType, entityTypeInfo);
                    }
            }
            return entityTypeInfos;
        }
        private EdmFunction BuildFunction(OeOperationConfiguration operationConfiguration, Dictionary<Type, EntityTypeInfo> entityTypeInfos)
        {
            IEdmTypeReference edmTypeReference;
            Type itemType = Parsers.OeExpressionHelper.GetCollectionItemType(operationConfiguration.ReturnType);
            if (itemType == null)
            {
                edmTypeReference = GetEdmTypeReference(operationConfiguration.ReturnType, entityTypeInfos);
                if (edmTypeReference == null)
                    return null;
            }
            else
            {
                edmTypeReference = GetEdmTypeReference(itemType, entityTypeInfos);
                if (edmTypeReference == null)
                    return null;

                edmTypeReference = new EdmCollectionTypeReference(new EdmCollectionType(edmTypeReference));
            }

            var edmFunction = new EdmFunction(operationConfiguration.NamespaceName, operationConfiguration.MethodInfoName, edmTypeReference);
            foreach (OeOperationParameterConfiguration parameterConfiguration in operationConfiguration.Parameters)
            {
                edmTypeReference = GetEdmTypeReference(parameterConfiguration.ClrType, entityTypeInfos);
                if (edmTypeReference == null)
                    return null;

                edmFunction.AddParameter(parameterConfiguration.Name, edmTypeReference);
            }

            return edmFunction;
        }
        private IEdmTypeReference GetEdmTypeReference(Type clrType, Dictionary<Type, EntityTypeInfo> entityTypeInfos)
        {
            bool nullable = PrimitiveTypeHelper.IsNullable(clrType);
            if (nullable)
            {
                Type underlyingType = Nullable.GetUnderlyingType(clrType);
                if (underlyingType != null)
                    clrType = underlyingType;
            }

            if (entityTypeInfos.TryGetValue(clrType, out EntityTypeInfo entityTypeInfo))
                return new EdmEntityTypeReference(entityTypeInfo.EdmType, nullable);

            if (_enumTypes.TryGetValue(clrType, out EdmEnumType edmEnumType))
                return new EdmEnumTypeReference(edmEnumType, nullable);

            if (_complexTypes.TryGetValue(clrType, out EdmComplexType edmComplexType))
                return new EdmComplexTypeReference(edmComplexType, nullable);

            return PrimitiveTypeHelper.GetPrimitiveTypeRef(clrType, nullable);
        }
        private static EdmStructuralProperty[] CreateDependentEdmProperties(EdmEntityType edmDependent, IReadOnlyList<PropertyInfo> dependentStructuralProperties)
        {
            if (dependentStructuralProperties.Count == 0)
                return null;

            EdmStructuralProperty[] dependentEdmProperties;
            dependentEdmProperties = new EdmStructuralProperty[dependentStructuralProperties.Count];
            for (int i = 0; i < dependentEdmProperties.Length; i++)
                dependentEdmProperties[i] = (EdmStructuralProperty)edmDependent.FindProperty(dependentStructuralProperties[i].Name);
            return dependentEdmProperties;
        }
        private static EdmNavigationProperty CreateNavigationProperty(FKeyInfo fkeyInfo)
        {
            EdmEntityType edmDependent = fkeyInfo.DependentInfo.EdmType;
            EdmEntityType edmPrincipal = fkeyInfo.PrincipalInfo.EdmType;

            EdmStructuralProperty[] dependentEdmProperties = CreateDependentEdmProperties(edmDependent, fkeyInfo.DependentStructuralProperties);

            EdmNavigationPropertyInfo edmPrincipalInfo;
            if (fkeyInfo.DependentNavigationProperty == null)
            {
                edmPrincipalInfo = new EdmNavigationPropertyInfo()
                {
                    ContainsTarget = false,
                    Name = fkeyInfo.PrincipalNavigationProperty.Name,
                    DependentProperties = dependentEdmProperties,
                    OnDelete = EdmOnDeleteAction.None,
                    PrincipalProperties = edmPrincipal.DeclaredKey,
                    Target = edmDependent,
                    TargetMultiplicity = fkeyInfo.PrincipalMultiplicity
                };
                return edmPrincipal.AddUnidirectionalNavigation(edmPrincipalInfo);
            }

            var edmDependentInfo = new EdmNavigationPropertyInfo()
            {
                ContainsTarget = false,
                Name = fkeyInfo.DependentNavigationProperty.Name,
                DependentProperties = dependentEdmProperties,
                OnDelete = EdmOnDeleteAction.None,
                PrincipalProperties = edmPrincipal.DeclaredKey,
                Target = edmPrincipal,
                TargetMultiplicity = fkeyInfo.DependentMultiplicity
            };

            if (fkeyInfo.PrincipalNavigationProperty == null || fkeyInfo.PrincipalNavigationProperty == fkeyInfo.DependentNavigationProperty)
                return edmDependent.AddUnidirectionalNavigation(edmDependentInfo);

            edmPrincipalInfo = new EdmNavigationPropertyInfo()
            {
                ContainsTarget = false,
                Name = fkeyInfo.PrincipalNavigationProperty.Name,
                DependentProperties = null,
                OnDelete = EdmOnDeleteAction.None,
                PrincipalProperties = edmPrincipal.DeclaredKey,
                Target = edmDependent,
                TargetMultiplicity = fkeyInfo.PrincipalMultiplicity
            };
            return edmDependent.AddBidirectionalNavigation(edmDependentInfo, edmPrincipalInfo);
        }
    }
}
