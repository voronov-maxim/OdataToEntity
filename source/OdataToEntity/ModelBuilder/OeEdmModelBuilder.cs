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

        public OeEdmModelBuilder(OeEdmModelMetadataProvider metadataProvider, IDictionary<String, Type> entitySets)
        {
            _entityTypes = new Dictionary<Type, EntityTypeInfo>(entitySets.Count);
            foreach (var pair in entitySets)
                _entityTypes.Add(pair.Value, new EntityTypeInfo(metadataProvider, pair.Value, pair.Key));

            _complexTypes = new Dictionary<Type, EdmComplexType>();
            _enumTypes = new Dictionary<Type, EdmEnumType>();
        }

        public void AddComplexType(Type complexType)
        {
            var edmComplexType = new EdmComplexType(complexType.Namespace, complexType.Name);
            _complexTypes.Add(complexType, edmComplexType);
        }
        public void AddEnumType(Type enumType)
        {
            var edmEnumType = new EdmEnumType(enumType.Namespace, enumType.Name);
            _enumTypes.Add(enumType, edmEnumType);
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
            edmModel.AddElements(_complexTypes.Values);
            var entitySets = new Dictionary<IEdmEntityType, EdmEntitySet>(_entityTypes.Count);
            foreach (EntityTypeInfo typeInfo in _entityTypes.Values)
            {
                edmModel.AddElement(typeInfo.EdmType);
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

            edmModel.AddElement(container);
            return edmModel;
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

            if (fkeyInfo.PrincipalNavigationProperty == null)
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
