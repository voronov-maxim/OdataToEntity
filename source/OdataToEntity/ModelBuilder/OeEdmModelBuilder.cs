using Microsoft.OData.Edm;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace OdataToEntity.ModelBuilder
{
    public sealed class OeEdmModelBuilder
    {
        private readonly Dictionary<Type, EdmComplexType> _complexTypes;
        private readonly Dictionary<Type, EntityTypeInfo> _entityTypes;
        private readonly Dictionary<Type, EdmEnumType> _enumTypes;
        private readonly List<OeOperationConfiguration> _operationConfigurations;

        public OeEdmModelBuilder(OeEdmModelMetadataProvider metadataProvider, IDictionary<String, Type> entitySets)
        {
            _entityTypes = new Dictionary<Type, EntityTypeInfo>(entitySets.Count);
            foreach (KeyValuePair<String, Type> pair in entitySets)
                _entityTypes.Add(pair.Value, new EntityTypeInfo(metadataProvider, pair.Value, pair.Key));

            _complexTypes = new Dictionary<Type, EdmComplexType>();
            _enumTypes = new Dictionary<Type, EdmEnumType>();
            _operationConfigurations = new List<OeOperationConfiguration>();
        }

        public OeOperationConfiguration AddFunction(String namespaceName, String name)
        {
            var functionConfiguration = new OeOperationConfiguration(namespaceName, name);
            _operationConfigurations.Add(functionConfiguration);
            return functionConfiguration;
        }
        public void AddComplexType(Type complexType)
        {
            var edmComplexType = new EdmComplexType(complexType.Namespace, complexType.Name);
            _complexTypes.Add(complexType, edmComplexType);
        }
        public void AddEnumType(Type enumType)
        {
            _enumTypes.Add(enumType, EntityTypeInfo.CreateEdmEnumType(enumType));
        }
        private EdmAction BuildAction(OeOperationConfiguration operationConfiguration)
        {
            var edmAction = new EdmAction(operationConfiguration.NamespaceName ?? "", operationConfiguration.Name, null);
            foreach (OeFunctionParameterConfiguration parameterConfiguration in operationConfiguration.Parameters)
            {
                IEdmTypeReference edmTypeReference = GetEdmTypeReference(parameterConfiguration.ClrType);
                if (edmTypeReference == null)
                    return null;

                edmAction.AddParameter(parameterConfiguration.Name, edmTypeReference);
            }

            return edmAction;
        }
        public EdmModel BuildEdmModel()
        {
            foreach (EntityTypeInfo typeInfo in _entityTypes.Values)
                typeInfo.BuildProperties(_entityTypes, _enumTypes, _complexTypes);

            foreach (EntityTypeInfo typeInfo in _entityTypes.Values)
                foreach (FKeyInfo fkeyInfo in typeInfo.NavigationClrProperties)
                    fkeyInfo.EdmNavigationProperty = CreateNavigationProperty(fkeyInfo);

            var edmModel = new EdmModel();
            var container = new EdmEntityContainer("Default", "Container");

            edmModel.AddElements(_enumTypes.Values);
            foreach (KeyValuePair<Type, EdmEnumType> enumType in _enumTypes)
                edmModel.SetAnnotationValue(enumType.Value, new OeClrTypeAnnotation(enumType.Key));

            edmModel.AddElements(_complexTypes.Values);
            foreach (KeyValuePair<Type, EdmComplexType> complexType in _complexTypes)
                edmModel.SetAnnotationValue(complexType.Value, new OeClrTypeAnnotation(complexType.Key));

            var entitySets = new Dictionary<IEdmEntityType, EdmEntitySet>(_entityTypes.Count);
            foreach (EntityTypeInfo typeInfo in _entityTypes.Values)
            {
                edmModel.AddElement(typeInfo.EdmType);
                edmModel.SetAnnotationValue(typeInfo.EdmType, new OeClrTypeAnnotation(typeInfo.ClrType));
                entitySets.Add(typeInfo.EdmType, container.AddEntitySet(typeInfo.EntitySetName, typeInfo.EdmType));
            }

            foreach (EntityTypeInfo typeInfo in _entityTypes.Values)
                foreach (FKeyInfo fkeyInfo in typeInfo.NavigationClrProperties)
                {
                    EdmEntitySet principal = entitySets[fkeyInfo.PrincipalInfo.EdmType];
                    EdmEntitySet dependent = entitySets[fkeyInfo.DependentInfo.EdmType];
                    dependent.AddNavigationTarget(fkeyInfo.EdmNavigationProperty, principal);

                    if (fkeyInfo.EdmNavigationProperty.Partner != null)
                        principal.AddNavigationTarget(fkeyInfo.EdmNavigationProperty.Partner, dependent);
                }


            foreach (OeOperationConfiguration operationConfiguration in _operationConfigurations)
            {
                if (operationConfiguration.IsFunction)
                {
                    EdmFunction edmFunction = BuildFunction(operationConfiguration);
                    if (edmFunction != null)
                        container.AddFunctionImport(edmFunction);
                }
                else
                {
                    EdmAction edmAction = BuildAction(operationConfiguration);
                    if (edmAction != null)
                        container.AddActionImport(edmAction);
                }
            }

            edmModel.AddElement(container);
            return edmModel;
        }
        private EdmFunction BuildFunction(OeOperationConfiguration functionConfiguration)
        {
            IEdmTypeReference edmTypeReference;
            Type itemType = Parsers.OeExpressionHelper.GetCollectionItemType(functionConfiguration.ReturnType);
            if (itemType == null)
            {
                edmTypeReference = GetEdmTypeReference(functionConfiguration.ReturnType);
                if (edmTypeReference == null)
                    return null;
            }
            else
            {
                edmTypeReference = GetEdmTypeReference(itemType);
                if (edmTypeReference == null)
                    return null;

                edmTypeReference = new EdmCollectionTypeReference(new EdmCollectionType(edmTypeReference));
            }

            var edmFunction = new EdmFunction(functionConfiguration.NamespaceName ?? "", functionConfiguration.Name, edmTypeReference);
            foreach (OeFunctionParameterConfiguration parameterConfiguration in functionConfiguration.Parameters)
            {
                edmTypeReference = GetEdmTypeReference(parameterConfiguration.ClrType);
                if (edmTypeReference == null)
                    return null;

                edmFunction.AddParameter(parameterConfiguration.Name, edmTypeReference);
            }

            return edmFunction;
        }
        private IEdmTypeReference GetEdmTypeReference(Type clrType)
        {
            bool nullable = PrimitiveTypeHelper.IsNullable(clrType);
            if (nullable)
            {
                Type underlyingType = Nullable.GetUnderlyingType(clrType);
                if (underlyingType != null)
                    clrType = underlyingType;
            }

            EntityTypeInfo entityTypeInfo;
            if (_entityTypes.TryGetValue(clrType, out entityTypeInfo))
                return new EdmEntityTypeReference(entityTypeInfo.EdmType, nullable);

            EdmEnumType edmEnumType;
            if (_enumTypes.TryGetValue(clrType, out edmEnumType))
                return new EdmEnumTypeReference(edmEnumType, nullable);

            EdmComplexType edmComplexType;
            if (_complexTypes.TryGetValue(clrType, out edmComplexType))
                return new EdmComplexTypeReference(edmComplexType, nullable);

            return PrimitiveTypeHelper.GetPrimitiveTypeRef(clrType, nullable);
        }
        private static EdmStructuralProperty[] CreateDependentEdmProperties(EdmEntityType edmDependent, IReadOnlyList<PropertyDescriptor> dependentStructuralProperties)
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

            var edmPrincipalInfo = new EdmNavigationPropertyInfo()
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
