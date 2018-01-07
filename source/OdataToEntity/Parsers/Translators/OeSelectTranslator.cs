using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace OdataToEntity.Parsers
{
    public sealed class OeSelectTranslator
    {
        private sealed class ParameterVisitor : ExpressionVisitor
        {
            private readonly Expression _newExpression;
            private readonly Expression _oldExpression;

            public ParameterVisitor(Expression oldExpression, Expression newExpression)
            {
                _oldExpression = oldExpression;
                _newExpression = newExpression;
            }

            protected override Expression VisitParameter(ParameterExpression node)
            {
                if (node == _oldExpression)
                    return _newExpression;
                return base.VisitParameter(node);
            }
        }

        private sealed class SelectItemInfo
        {
            private readonly bool? _countOption;
            private readonly IEdmProperty _edmProperty;
            private readonly IEdmEntitySet _entitySet;
            private readonly bool _pathSelect;
            private readonly ODataNestedResourceInfo _resource;

            public SelectItemInfo(IEdmEntitySet entitySet, IEdmProperty edmProperty, ODataNestedResourceInfo resource, bool pathSelect, bool? countOption)
            {
                _entitySet = entitySet;
                _edmProperty = edmProperty;
                _resource = resource;
                _pathSelect = pathSelect;
                _countOption = countOption;
            }

            public bool? CountOption => _countOption;
            public IEdmProperty EdmProperty => _edmProperty;
            public IEdmEntitySet EntitySet => _entitySet;
            public OeEntryFactory EntryFactory { get; set; }
            public ExpandedNavigationSelectItem ExpandedNavigationSelectItem { get; set; }
            public int ExpressionIndex { get; set; }
            public bool PathSelect => _pathSelect;
            public ODataNestedResourceInfo ResourceInfo => _resource;
        }

        private sealed class SelectItemTranslator : SelectItemTranslator<Expression>
        {
            private readonly OeMetadataLevel _metadataLevel;
            private readonly IEdmModel _model;
            private readonly bool _navigationNextLink;
            private readonly ODataPath _path;
            private readonly ParameterExpression _parameter;
            private SelectItemInfo _selectItemInfo;
            private readonly Expression _source;
            private readonly OeQueryNodeVisitor _visitor;

            public SelectItemTranslator(OeQueryNodeVisitor visitor, ODataPath path, OeMetadataLevel metadataLevel, bool navigationNextLink,
                ParameterExpression parameter, Expression source)
            {
                _visitor = visitor;
                _path = path;
                _metadataLevel = metadataLevel;
                _navigationNextLink = navigationNextLink;
                _parameter = parameter;
                _source = source;
                _model = visitor.EdmModel;
            }

            private static SelectItemInfo CreateNavigationSelectItemInfo(IEdmModel model, NavigationPropertySegment segment, bool pathSelect, bool? countOption)
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
                    foreach (IEdmEntitySet element in model.EntityContainer.EntitySets())
                        if (element.EntityType() == entityType)
                        {
                            entitySet = element;
                            break;
                        }
                }
                return new SelectItemInfo(entitySet, navigationEdmProperty, resourceInfo, pathSelect, countOption);
            }
            public override Expression Translate(ExpandedNavigationSelectItem item)
            {
                var segment = (NavigationPropertySegment)item.PathToNavigationProperty.LastSegment;
                if (_navigationNextLink && segment.NavigationProperty.Type.Definition is IEdmCollectionType)
                    return null;

                _selectItemInfo = CreateNavigationSelectItemInfo(_model, segment, false, item.CountOption);

                PropertyInfo navigationClrProperty = _parameter.Type.GetProperty(_selectItemInfo.EdmProperty.Name);
                Expression expression = Expression.MakeMemberAccess(_parameter, navigationClrProperty);

                Type navigationItemType = expression.Type;
                Type itemType = OeExpressionHelper.GetCollectionItemType(navigationItemType);
                if (itemType != null)
                {
                    var expressionBuilder = new OeExpressionBuilder(_model, itemType);
                    expression = expressionBuilder.ApplyFilter(expression, item.FilterOption);
                    expression = expressionBuilder.ApplyOrderBy(expression, item.OrderByOption);

                    var path = new ODataPath(_path.Union(item.PathToNavigationProperty));
                    expression = expressionBuilder.ApplySkip(expression, item.SkipOption, path);
                    expression = expressionBuilder.ApplyTake(expression, item.TopOption, path);

                    foreach (KeyValuePair<ConstantExpression, ConstantNode> constant in expressionBuilder.Constants)
                        _visitor.AddConstant(constant.Key, constant.Value);
                }

                if (item.SelectAndExpand.SelectedItems.Any())
                {
                    var path = new ODataPath(_path.Union(item.PathToNavigationProperty));
                    var selectTranslator = new OeSelectTranslator(_visitor, path, _metadataLevel, _navigationNextLink);
                    Expression nestedExpression = selectTranslator.CreateExpression(expression, item.SelectAndExpand);

                    Type nestedType = OeExpressionHelper.GetCollectionItemType(nestedExpression.Type);
                    if (nestedType == null)
                    {
                        var visitor = new ParameterVisitor(selectTranslator._parameter, expression);
                        nestedExpression = visitor.Visit(nestedExpression);
                        nestedType = nestedExpression.Type;
                    }

                    _selectItemInfo.EntryFactory = selectTranslator.CreateNestedEntryFactory(nestedType, _selectItemInfo.EntitySet, _selectItemInfo.ResourceInfo);
                    expression = nestedExpression;
                }

                return expression;
            }
            public override Expression Translate(PathSelectItem item)
            {
                Expression expression;
                if (item.SelectedPath.LastSegment is NavigationPropertySegment navigationSegment)
                {
                    if (_navigationNextLink && navigationSegment.NavigationProperty.Type.Definition is IEdmCollectionType)
                        return null;

                    _selectItemInfo = CreateNavigationSelectItemInfo(_model, navigationSegment, true, null);

                    PropertyInfo navigationClrProperty = _parameter.Type.GetProperty(_selectItemInfo.EdmProperty.Name);
                    expression = Expression.MakeMemberAccess(_parameter, navigationClrProperty);
                }
                else if (item.SelectedPath.LastSegment is PropertySegment propertySegment)
                {
                    _selectItemInfo = new SelectItemInfo(null, propertySegment.Property, null, true, null);

                    PropertyInfo property = _parameter.Type.GetProperty(propertySegment.Property.Name);
                    if (property == null)
                        expression = new TuplePropertyByEdmProperty(_source).GetTuplePropertyByEdmProperty(_parameter, propertySegment.Property);
                    else
                        expression = Expression.MakeMemberAccess(_parameter, property);
                }
                else
                    throw new InvalidOperationException(item.SelectedPath.LastSegment.GetType().Name + " not supported");

                return expression;
            }

            public SelectItemInfo SelectItemInfo => _selectItemInfo;
        }

        private readonly IEdmModel _model;
        private ParameterExpression _parameter;
        private readonly ODataPath _path;
        private readonly OeMetadataLevel _metadataLevel;
        private readonly bool _navigationNextLink;
        private readonly List<SelectItemInfo> _selectItemInfos;
        private Expression _source;
        private readonly OeQueryNodeVisitor _visitor;

        public OeSelectTranslator(OeQueryNodeVisitor visitor, ODataPath path, OeMetadataLevel metadataLevel, bool navigationNextLink)
        {
            _visitor = visitor;
            _path = path;
            _metadataLevel = metadataLevel;
            _navigationNextLink = navigationNextLink;
            _model = visitor.EdmModel;
            _selectItemInfos = new List<SelectItemInfo>();
        }

        private void AddKey(Type itemType, List<Expression> expressions)
        {
            var edmEnityType = (IEdmEntityType)_model.FindType(itemType.FullName);
            foreach (IEdmStructuralProperty keyProperty in edmEnityType.DeclaredKey)
            {
                if (SelectItemInfoExists(keyProperty))
                    continue;

                var selectItemInfo = new SelectItemInfo(null, keyProperty, null, true, null) { ExpressionIndex = expressions.Count };
                _selectItemInfos.Add(selectItemInfo);

                PropertyInfo property = itemType.GetProperty(keyProperty.Name);
                expressions.Add(Expression.MakeMemberAccess(_parameter, property));
            }
        }
        public Expression Build(Expression source, SelectExpandClause selectClause)
        {
            if (selectClause == null)
                return source;

            return (MethodCallExpression)CreateExpression(source, selectClause);
        }
        public OeEntryFactory CreateEntryFactory(Type entityType, IEdmEntitySet entitySet, Type sourceType)
        {
            ParameterExpression parameter = Expression.Parameter(typeof(Object));
            IReadOnlyList<MemberExpression> itemExpressions = OeExpressionHelper.GetPropertyExpression(Expression.Convert(parameter, sourceType));

            OeEntryFactory entryFactory;
            List<OeEntryFactory> navigationLinks = GetNavigationLinks(itemExpressions, parameter);
            if (_selectItemInfos.Any(i => i.PathSelect))
            {
                var accessors = new List<OePropertyAccessor>(_selectItemInfos.Count);
                foreach (SelectItemInfo selectItemInfo in _selectItemInfos)
                    if (selectItemInfo.EdmProperty is IEdmStructuralProperty)
                        accessors.Add(OePropertyAccessor.CreatePropertyAccessor(selectItemInfo.EdmProperty, itemExpressions[selectItemInfo.ExpressionIndex], parameter));
                entryFactory = OeEntryFactory.CreateEntryFactoryParent(entitySet, accessors.ToArray(), navigationLinks);
            }
            else
            {
                OePropertyAccessor[] accessors = OePropertyAccessor.CreateFromType(entityType, entitySet);
                entryFactory = OeEntryFactory.CreateEntryFactoryParent(entitySet, accessors, navigationLinks);
                entryFactory.LinkAccessor = (Func<Object, Object>)Expression.Lambda(itemExpressions[0], parameter).Compile();
            }
            return entryFactory;
        }
        private Expression CreateExpression(Expression source, SelectExpandClause selectClause)
        {
            _source = source;
            Type itemType = OeExpressionHelper.GetCollectionItemType(source.Type);
            _parameter = Expression.Parameter(itemType ?? source.Type);

            var expressions = new List<Expression>();
            foreach (SelectItem selectItem in selectClause.SelectedItems)
            {
                var selectItemTranslator = new SelectItemTranslator(_visitor, _path, _metadataLevel, _navigationNextLink, _parameter, _source);
                Expression expression = selectItem.TranslateWith(selectItemTranslator);
                if (expression == null || SelectItemInfoExists(selectItemTranslator.SelectItemInfo.EdmProperty))
                    continue;

                selectItemTranslator.SelectItemInfo.ExpressionIndex = expressions.Count;
                expressions.Add(expression);
                _selectItemInfos.Add(selectItemTranslator.SelectItemInfo);
            }

            if (_selectItemInfos.Any(i => i.PathSelect))
            {
                if (_metadataLevel == OeMetadataLevel.Full)
                    AddKey(itemType, expressions);
            }
            else
            {
                expressions.Insert(0, _parameter);
                foreach (SelectItemInfo selectItemInfo in _selectItemInfos)
                    selectItemInfo.ExpressionIndex++;
            }

            NewExpression newExpression = OeExpressionHelper.CreateTupleExpression(expressions);
            if (itemType == null)
                return newExpression;

            LambdaExpression lambda = Expression.Lambda(newExpression, _parameter);
            MethodInfo selectMethodInfo = OeMethodInfoHelper.GetSelectMethodInfo(_parameter.Type, newExpression.Type);
            return Expression.Call(selectMethodInfo, source, lambda);
        }
        private OeEntryFactory CreateNestedEntryFactory(Type sourceType, IEdmEntitySet entitySet, ODataNestedResourceInfo resourceInfo)
        {
            ParameterExpression parameter = Expression.Parameter(typeof(Object));
            IReadOnlyList<MemberExpression> itemExpressions = OeExpressionHelper.GetPropertyExpression(Expression.Convert(parameter, sourceType));

            OePropertyAccessor[] accessors;
            if (_selectItemInfos.Any(i => i.PathSelect))
            {
                var accessorsList = new List<OePropertyAccessor>(_selectItemInfos.Count);
                foreach (SelectItemInfo selectItemInfo in _selectItemInfos)
                    if (selectItemInfo.EdmProperty is IEdmStructuralProperty)
                        accessorsList.Add(OePropertyAccessor.CreatePropertyAccessor(selectItemInfo.EdmProperty, itemExpressions[selectItemInfo.ExpressionIndex], parameter));
                accessors = accessorsList.ToArray();
            }
            else
                accessors = OePropertyAccessor.CreateFromExpression(itemExpressions[0], parameter, entitySet);

            List<OeEntryFactory> navigationLinks = GetNavigationLinks(itemExpressions, parameter);
            return OeEntryFactory.CreateEntryFactoryNested(entitySet, accessors, resourceInfo, navigationLinks);
        }
        private List<OeEntryFactory> GetNavigationLinks(IReadOnlyList<MemberExpression> itemExpressions, ParameterExpression parameter)
        {
            var navigationLinks = new List<OeEntryFactory>(_selectItemInfos.Count);
            foreach (SelectItemInfo itemInfo in _selectItemInfos)
                if (itemInfo.EdmProperty is IEdmNavigationProperty)
                {
                    OeEntryFactory entryFactory;
                    if (itemInfo.EntryFactory == null)
                    {
                        Type type = itemExpressions[itemInfo.ExpressionIndex].Type;
                        if (itemInfo.ResourceInfo.IsCollection.GetValueOrDefault())
                            type = OeExpressionHelper.GetCollectionItemType(type);

                        OePropertyAccessor[] accessors = OePropertyAccessor.CreateFromType(type, itemInfo.EntitySet);
                        entryFactory = OeEntryFactory.CreateEntryFactoryChild(itemInfo.EntitySet, accessors, itemInfo.ResourceInfo);
                        entryFactory.CountOption = itemInfo.CountOption;
                    }
                    else
                        entryFactory = itemInfo.EntryFactory;
                    entryFactory.LinkAccessor = (Func<Object, Object>)Expression.Lambda(itemExpressions[itemInfo.ExpressionIndex], parameter).Compile();
                    navigationLinks.Add(entryFactory);
                }

            return navigationLinks;
        }
        private bool SelectItemInfoExists(IEdmProperty edmProperty)
        {
            for (int i = 0; i < _selectItemInfos.Count; i++)
                if (_selectItemInfos[i].EdmProperty == edmProperty)
                    return true;
            return false;
        }
    }
}
