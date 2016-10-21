using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace OdataToEntity.Parsers
{
    public sealed class OeSelectTranslator : SelectItemTranslator<Expression>
    {
        private struct SelectItemInfo
        {
            private readonly IEdmProperty _edmProperty;
            private readonly IEdmEntitySet _entitySet;
            private readonly ODataNestedResourceInfo _resource;

            public SelectItemInfo(IEdmEntitySet entitySet, IEdmProperty edmProperty, ODataNestedResourceInfo resource)
            {
                _entitySet = entitySet;
                _edmProperty = edmProperty;
                _resource = resource;
            }

            public IEdmProperty EdmProperty => _edmProperty;
            public IEdmEntitySet EntitySet => _entitySet;
            public ODataNestedResourceInfo Resource => _resource;
        }

        private readonly IEdmModel _model;
        private ParameterExpression _parameter;
        private bool _select;
        private SelectItemInfo _selectItemInfo;
        private readonly List<SelectItemInfo> _selectItemInfos;

        public OeSelectTranslator(IEdmModel model)
        {
            _model = model;
            _selectItemInfos = new List<SelectItemInfo>();
        }

        private void AddKey(Type itemType, List<Expression> expressions)
        {
            var edmEnityType = (IEdmEntityType)_model.FindType(itemType.FullName);
            foreach (IEdmStructuralProperty keyProperty in edmEnityType.DeclaredKey)
            {
                if (SelectItemInfoExists(keyProperty))
                    continue;

                _selectItemInfos.Add(new SelectItemInfo(null, keyProperty, null));
                PropertyInfo property = itemType.GetTypeInfo().GetProperty(keyProperty.Name);
                expressions.Add(Expression.MakeMemberAccess(_parameter, property));
            }
        }
        public MethodCallExpression Build(MethodCallExpression source, SelectExpandClause selectClause, OeMetadataLevel metadatLevel)
        {
            _select = false;
            _selectItemInfos.Clear();
            if (selectClause == null)
                return source;

            Type itemType = OeExpressionHelper.GetCollectionItemType(source.Type);
            _parameter = Expression.Parameter(itemType);
            var expressions = new List<Expression>();
            foreach (var selectItem in selectClause.SelectedItems)
            {
                Expression expression = selectItem.TranslateWith(this);
                if (SelectItemInfoExists(_selectItemInfo.EdmProperty))
                    continue;

                expressions.Add(expression);
                _selectItemInfos.Add(_selectItemInfo);
            }

            if (_select)
            {
                if (metadatLevel == OeMetadataLevel.Full)
                    AddKey(itemType, expressions);
            }
            else
                expressions.Insert(0, _parameter);

            NewExpression newExpression = OeExpressionHelper.CreateTupleExpression(expressions);
            LambdaExpression lambda = Expression.Lambda(newExpression, _parameter);
            MethodInfo selectMethodInfo = OeMethodInfoHelper.GetSelectMethodInfo(_parameter.Type, newExpression.Type);
            return Expression.Call(selectMethodInfo, source, lambda);
        }
        public OeEntryFactory CreateEntryFactory(Type entityType, IEdmEntitySet entitySet, Type sourceType)
        {
            ParameterExpression parameter = Expression.Parameter(typeof(Object));
            IReadOnlyList<MemberExpression> itemExpressions = OeExpressionHelper.GetPropertyExpression(Expression.Convert(parameter, sourceType));

            var navigationLinks = new List<OeEntryFactory>(_selectItemInfos.Count);
            for (int i = 0; i < _selectItemInfos.Count; i++)
            {
                SelectItemInfo info = _selectItemInfos[i];
                if (info.EdmProperty is IEdmNavigationProperty)
                {
                    MemberExpression expression = itemExpressions[_select ? i : i + 1];
                    Type type = expression.Type;
                    if (info.Resource.IsCollection.GetValueOrDefault())
                        type = OeExpressionHelper.GetCollectionItemType(type);

                    OePropertyAccessor[] accessors = OePropertyAccessor.CreateFromType(type, info.EntitySet);
                    Func<Object, Object> linkAccessor = (Func<Object, Object>)Expression.Lambda(expression, parameter).Compile();
                    navigationLinks.Add(OeEntryFactory.CreateEntryFactoryChild(info.EntitySet, accessors, linkAccessor, info.Resource));
                }
            }

            if (_select)
            {
                var accessors = new List<OePropertyAccessor>(_selectItemInfos.Count);
                for (int i = 0; i < _selectItemInfos.Count; i++)
                {
                    SelectItemInfo info = _selectItemInfos[i];
                    if (info.EdmProperty is IEdmStructuralProperty)
                        accessors.Add(OePropertyAccessor.CreatePropertyAccessor(info.EdmProperty, itemExpressions[i], parameter));
                }
                return OeEntryFactory.CreateEntryFactoryParent(entitySet, accessors.ToArray(), null, navigationLinks);
            }
            else
            {
                var linkAccessor = (Func<Object, Object>)Expression.Lambda(itemExpressions[0], parameter).Compile();
                OePropertyAccessor[] accessors = OePropertyAccessor.CreateFromType(entityType, entitySet);
                return OeEntryFactory.CreateEntryFactoryParent(entitySet, accessors, linkAccessor, navigationLinks);
            }
        }
        private bool SelectItemInfoExists(IEdmProperty edmProperty)
        {
            for (int i = 0; i < _selectItemInfos.Count; i++)
                if (_selectItemInfos[i].EdmProperty == edmProperty)
                    return true;
            return false;
        }
        private Expression Translate(NavigationPropertySegment segment)
        {
            IEdmNavigationProperty navigationEdmProperty = segment.NavigationProperty;
            var collectionType = navigationEdmProperty.Type.Definition as IEdmCollectionType;

            var resourceInfo = new ODataNestedResourceInfo()
            {
                IsCollection = collectionType != null,
                Name = navigationEdmProperty.Name
            };

            var entitySet = (IEdmEntitySet)segment.NavigationSource;
            if (entitySet == null)
            {
                IEdmType entityType;
                if (collectionType == null)
                    entityType = navigationEdmProperty.Type.Definition;
                else
                    entityType = collectionType.ElementType.Definition;
                foreach (IEdmEntitySet element in _model.EntityContainer.Elements)
                    if (element.EntityType() == entityType)
                    {
                        entitySet = element;
                        break;
                    }
            }
            _selectItemInfo = new SelectItemInfo(entitySet, navigationEdmProperty, resourceInfo);

            PropertyInfo navigationClrProperty = _parameter.Type.GetTypeInfo().GetProperty(navigationEdmProperty.Name);
            return Expression.MakeMemberAccess(_parameter, navigationClrProperty);
        }
        public override Expression Translate(ExpandedNavigationSelectItem item)
        {
            return Translate((NavigationPropertySegment)item.PathToNavigationProperty.LastSegment);
        }
        public override Expression Translate(PathSelectItem item)
        {
            _select = true;
            Expression expression;
            if (item.SelectedPath.LastSegment is NavigationPropertySegment)
            {
                var segment = (NavigationPropertySegment)item.SelectedPath.LastSegment;
                expression = Translate(segment);
            }
            else if (item.SelectedPath.LastSegment is PropertySegment)
            {
                var segment = (PropertySegment)item.SelectedPath.LastSegment;
                _selectItemInfo = new SelectItemInfo(null, segment.Property, null);

                PropertyInfo property = _parameter.Type.GetTypeInfo().GetProperty(segment.Property.Name);
                expression = Expression.MakeMemberAccess(_parameter, property);
            }
            else
                throw new InvalidOperationException(item.SelectedPath.LastSegment.GetType().Name + " not suppoerted");

            return expression;
        }
    }
}
