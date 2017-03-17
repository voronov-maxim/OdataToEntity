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
    public sealed class OeSelectTranslator : SelectItemTranslator<Expression>
    {
        private struct SelectItemInfo
        {
            private readonly bool? _countOption;
            private readonly IEdmProperty _edmProperty;
            private readonly IEdmEntitySet _entitySet;
            private readonly ODataNestedResourceInfo _resource;

            public SelectItemInfo(IEdmEntitySet entitySet, IEdmProperty edmProperty, ODataNestedResourceInfo resource, bool? countOption)
            {
                _entitySet = entitySet;
                _edmProperty = edmProperty;
                _resource = resource;
                _countOption = countOption;
                EntryFactory = null;
            }

            public bool? CountOption => _countOption;
            public IEdmProperty EdmProperty => _edmProperty;
            public IEdmEntitySet EntitySet => _entitySet;
            public OeEntryFactory EntryFactory { get; set; }
            public ODataNestedResourceInfo ResourceInfo => _resource;
        }

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

        private readonly IEdmModel _model;
        private ParameterExpression _parameter;
        private readonly ODataPath _path;
        private bool _pathSelect;
        private SelectItemInfo _selectItemInfo;
        private readonly List<SelectItemInfo> _selectItemInfos;
        private readonly OeQueryNodeVisitor _visitor;

        public OeSelectTranslator(OeQueryNodeVisitor visitor, ODataPath path)
        {
            _visitor = visitor;
            _path = path;
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

                _selectItemInfos.Add(new SelectItemInfo(null, keyProperty, null, null));
                PropertyInfo property = itemType.GetTypeInfo().GetProperty(keyProperty.Name);
                expressions.Add(Expression.MakeMemberAccess(_parameter, property));
            }
        }
        public Expression Build(Expression source, SelectExpandClause selectClause, OeMetadataLevel metadatLevel)
        {
            _pathSelect = false;
            _selectItemInfos.Clear();
            if (selectClause == null)
                return source;

            return (MethodCallExpression)CreateExpression(source, selectClause, metadatLevel);
        }
        public OeEntryFactory CreateEntryFactory(Type entityType, IEdmEntitySetBase entitySet, Type sourceType)
        {
            ParameterExpression parameter = Expression.Parameter(typeof(Object));
            IReadOnlyList<MemberExpression> itemExpressions = OeExpressionHelper.GetPropertyExpression(Expression.Convert(parameter, sourceType));

            OeEntryFactory entryFactory;
            List<OeEntryFactory> navigationLinks = GetNavigationLinks(itemExpressions, parameter);
            if (_pathSelect)
            {
                var accessors = new List<OePropertyAccessor>(_selectItemInfos.Count);
                for (int i = 0; i < _selectItemInfos.Count; i++)
                {
                    SelectItemInfo info = _selectItemInfos[i];
                    if (info.EdmProperty is IEdmStructuralProperty)
                        accessors.Add(OePropertyAccessor.CreatePropertyAccessor(info.EdmProperty, itemExpressions[i], parameter));
                }
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
        private Expression CreateExpression(Expression source, SelectExpandClause selectClause, OeMetadataLevel metadatLevel)
        {
            Type itemType = OeExpressionHelper.GetCollectionItemType(source.Type);
            if (itemType == null)
                _parameter = Expression.Parameter(source.Type);
            else
                _parameter = Expression.Parameter(itemType);

            var expressions = new List<Expression>();
            foreach (SelectItem selectItem in selectClause.SelectedItems)
            {
                Expression expression = selectItem.TranslateWith(this);
                if (SelectItemInfoExists(_selectItemInfo.EdmProperty))
                    continue;

                expressions.Add(expression);
                _selectItemInfos.Add(_selectItemInfo);
            }

            if (_pathSelect)
            {
                if (metadatLevel == OeMetadataLevel.Full)
                    AddKey(itemType, expressions);
            }
            else
                expressions.Insert(0, _parameter);

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

            List<OeEntryFactory> navigationLinks = GetNavigationLinks(itemExpressions, parameter);
            OePropertyAccessor[] accessors;
            if (_pathSelect)
            {
                var accessorsList = new List<OePropertyAccessor>(_selectItemInfos.Count);
                for (int i = 0; i < _selectItemInfos.Count; i++)
                {
                    SelectItemInfo info = _selectItemInfos[i];
                    if (info.EdmProperty is IEdmStructuralProperty)
                        accessorsList.Add(OePropertyAccessor.CreatePropertyAccessor(info.EdmProperty, itemExpressions[i], parameter));
                }
                accessors = accessorsList.ToArray();
            }
            else
                accessors = OePropertyAccessor.CreateFromExpression(itemExpressions[0], parameter, entitySet);

            return OeEntryFactory.CreateEntryFactoryNested(entitySet, accessors, resourceInfo, navigationLinks);
        }
        private List<OeEntryFactory> GetNavigationLinks(IReadOnlyList<MemberExpression> itemExpressions, ParameterExpression parameter)
        {
            var navigationLinks = new List<OeEntryFactory>(_selectItemInfos.Count);
            for (int i = 0; i < _selectItemInfos.Count; i++)
            {
                SelectItemInfo itemInfo = _selectItemInfos[i];
                if (itemInfo.EdmProperty is IEdmNavigationProperty)
                {
                    MemberExpression expression = itemExpressions[_pathSelect ? i : i + 1];

                    OeEntryFactory entryFactory;
                    if (itemInfo.EntryFactory == null)
                    {
                        Type type = expression.Type;
                        if (itemInfo.ResourceInfo.IsCollection.GetValueOrDefault())
                            type = OeExpressionHelper.GetCollectionItemType(type);

                        OePropertyAccessor[] accessors = OePropertyAccessor.CreateFromType(type, itemInfo.EntitySet);
                        entryFactory = OeEntryFactory.CreateEntryFactoryChild(itemInfo.EntitySet, accessors, itemInfo.ResourceInfo);
                        entryFactory.CountOption = itemInfo.CountOption;
                    }
                    else
                        entryFactory = itemInfo.EntryFactory;
                    entryFactory.LinkAccessor = (Func<Object, Object>)Expression.Lambda(expression, parameter).Compile();

                    navigationLinks.Add(entryFactory);
                }
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
        public override Expression Translate(ExpandedNavigationSelectItem item)
        {
            var segment = (NavigationPropertySegment)item.PathToNavigationProperty.LastSegment;
            Expression expression = Translate(segment, item.CountOption);
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
                var selectTranslator = new OeSelectTranslator(_visitor, path);
                Expression nestedExpression = selectTranslator.CreateExpression(expression, item.SelectAndExpand, OeMetadataLevel.Minimal);

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
        private Expression Translate(NavigationPropertySegment segment, bool? countOption)
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
            _selectItemInfo = new SelectItemInfo(entitySet, navigationEdmProperty, resourceInfo, countOption);

            PropertyInfo navigationClrProperty = _parameter.Type.GetTypeInfo().GetProperty(navigationEdmProperty.Name);
            return Expression.MakeMemberAccess(_parameter, navigationClrProperty);
        }
        public override Expression Translate(PathSelectItem item)
        {
            _pathSelect = true;
            Expression expression;
            if (item.SelectedPath.LastSegment is NavigationPropertySegment)
            {
                var segment = (NavigationPropertySegment)item.SelectedPath.LastSegment;
                expression = Translate(segment, null);
            }
            else if (item.SelectedPath.LastSegment is PropertySegment)
            {
                var segment = (PropertySegment)item.SelectedPath.LastSegment;
                _selectItemInfo = new SelectItemInfo(null, segment.Property, null, null);

                PropertyInfo property = _parameter.Type.GetTypeInfo().GetProperty(segment.Property.Name);
                expression = Expression.MakeMemberAccess(_parameter, property);
            }
            else
                throw new InvalidOperationException(item.SelectedPath.LastSegment.GetType().Name + " not supported");

            return expression;
        }
    }
}
