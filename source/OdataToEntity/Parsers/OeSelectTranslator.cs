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
        private IEdmEntitySet _currentEnitySet;
        private ODataNestedResourceInfo _currentLink;
        private Expression _expression;
        private Type _lastNavigationType;
        private Func<Object, Object> _linkAccessor;
        private OeEntryFactory[] _navigationLinks;

        private MemberInitExpression CreateExpression(List<ExpandedNavigationSelectItem> expandedItems)
        {
            Expression[] expressions = new Expression[expandedItems.Count + 1];
            ODataNestedResourceInfo[] links = new ODataNestedResourceInfo[expandedItems.Count];
            IEdmEntitySet[] entitySets = new IEdmEntitySet[expandedItems.Count];

            expressions[0] = _expression;
            for (int i = 0; i < expandedItems.Count; i++)
            {
                expressions[i + 1] = expandedItems[i].TranslateWith(this);
                links[i] = _currentLink;
                entitySets[i] = _currentEnitySet;
            }

            ParameterExpression parameter = Expression.Parameter(typeof(Object));
            MemberInitExpression initExpression = OeExpressionHelper.CreateTupleExpression(expressions);
            MemberExpression[] itemExpressions = OeExpressionHelper.GetPropertyExpression(Expression.Convert(parameter, initExpression.Type));

            _navigationLinks = new OeEntryFactory[expandedItems.Count];
            for (int i = 0; i < _navigationLinks.Length; i++)
            {
                Type type = expressions[i + 1].Type;
                if (links[i].IsCollection.GetValueOrDefault())
                    type = OeExpressionHelper.GetCollectionItemType(type);

                OePropertyAccessor[] accessors = OePropertyAccessor.CreateFromType(type, entitySets[i]);
                Func<Object, Object> linkAccessor = (Func<Object, Object>)Expression.Lambda(itemExpressions[i + 1], parameter).Compile();
                _navigationLinks[i] = OeEntryFactory.CreateEntryFactoryChild(entitySets[i], accessors, linkAccessor, links[i]);
            }

            _linkAccessor = (Func<Object, Object>)Expression.Lambda(itemExpressions[0], parameter).Compile();
            return initExpression;
        }
        public override Expression Translate(ExpandedNavigationSelectItem item)
        {
            IEdmNavigationProperty navigationEdmProperty = ((NavigationPropertySegment)item.PathToNavigationProperty.LastSegment).NavigationProperty;
            var collectionType = navigationEdmProperty.Type.Definition as IEdmCollectionType;

            _currentEnitySet = (IEdmEntitySet)item.NavigationSource;
            _currentLink = new ODataNestedResourceInfo()
            {
                IsCollection = collectionType != null,
                Name = navigationEdmProperty.Name
            };

            PropertyInfo navigationClrProperty = _expression.Type.GetTypeInfo().GetProperty(navigationEdmProperty.Name);
            return Expression.MakeMemberAccess(_expression, navigationClrProperty);
        }
        public override Expression Translate(PathSelectItem item)
        {
            if (item.SelectedPath.LastSegment is NavigationPropertySegment)
            {
                var navigationSeg = (NavigationPropertySegment)item.SelectedPath.LastSegment;
                PropertyInfo property = _expression.Type.GetTypeInfo().GetProperty(navigationSeg.NavigationProperty.Name);
                return Expression.MakeMemberAccess(_expression, property);
            }
            else if (item.SelectedPath.LastSegment is PropertySegment)
            {
                //var propertySeg = (PropertySegment)item.SelectedPath.LastSegment;
                //PropertyInfo property = _parameter.Type.GetTypeInfo().GetProperty(propertySeg.Property.Name);
                //return Expression.MakeMemberAccess(_parameter, property);
                return _expression;
            }

            throw new InvalidOperationException(item.SelectedPath.LastSegment.GetType().Name + " not suppoerted");
        }
        public MethodCallExpression Build(MethodCallExpression source, SelectExpandClause selectClause)
        {
            if (selectClause == null)
                return source;

            Type itemType = OeExpressionHelper.GetCollectionItemType(source.Type);
            ParameterExpression parameter = Expression.Parameter(itemType);
            _expression = parameter;

            List<PathSelectItem> pathSelects = selectClause.SelectedItems.OfType<PathSelectItem>().ToList();
            if (pathSelects.Count > 0)
            {
                foreach (PathSelectItem pathSelectItem in pathSelects)
                    _expression = Translate(pathSelectItem);

                LambdaExpression lambda = Expression.Lambda(_expression, parameter);
                MethodInfo selectMethodInfo;
                itemType = OeExpressionHelper.GetCollectionItemType(_expression.Type);
                if (itemType == null)
                {
                    selectMethodInfo = OeMethodInfoHelper.GetSelectMethodInfo(parameter.Type, _expression.Type);
                    itemType = _expression.Type;
                }
                else
                    selectMethodInfo = OeMethodInfoHelper.GetSelectManyMethodInfo(parameter.Type, itemType);

                _lastNavigationType = itemType;
                parameter = Expression.Parameter(itemType);
                _expression = parameter;
                source = Expression.Call(selectMethodInfo, source, lambda);
            }

            List<ExpandedNavigationSelectItem> expandeds = selectClause.SelectedItems.OfType<ExpandedNavigationSelectItem>().ToList();
            if (expandeds.Count > 0)
            {
                MemberInitExpression initExpression = CreateExpression(expandeds);
                LambdaExpression lambda = Expression.Lambda(initExpression, parameter);
                MethodInfo selectMethodInfo = OeMethodInfoHelper.GetSelectMethodInfo(_expression.Type, initExpression.Type);
                source = Expression.Call(selectMethodInfo, source, lambda);
            }

            return source;
        }
        public OeEntryFactory CreateEntryFactory(Type entityType, IEdmEntitySet entitySet, Type sourceType)
        {
            OePropertyAccessor[] accessors = OePropertyAccessor.CreateFromType(entityType, entitySet);
            return OeEntryFactory.CreateEntryFactoryParent(entitySet, accessors, _linkAccessor, _navigationLinks);
        }

        public Type LastNavigationType => _lastNavigationType;
    }
}
