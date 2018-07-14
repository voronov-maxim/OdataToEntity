using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace OdataToEntity.Parsers.Translators
{
    public sealed class OeSelectTranslator
    {
        private sealed class ComputeProperty : IEdmProperty
        {
            public ComputeProperty(String alias, IEdmTypeReference edmTypeReference)
            {
                Name = alias;
                Type = edmTypeReference;
            }

            public IEdmStructuredType DeclaringType => ModelBuilder.PrimitiveTypeHelper.TupleEdmType;
            public String Name { get; }
            public EdmPropertyKind PropertyKind => throw new NotSupportedException();
            public IEdmTypeReference Type { get; }
        }

        private readonly OeSelectItem _navigationItem;
        private readonly OeQueryNodeVisitor _visitor;
        private OeEntryFactory _entryFactory;

        public OeSelectTranslator(OeQueryNodeVisitor visitor, ODataPath path) :
            this(visitor, new OeSelectItem(null, GetEntitySet(path), path, null, null, null, false))
        {
        }
        internal OeSelectTranslator(OeQueryNodeVisitor visitor, OeSelectItem navigationItem)
        {
            _visitor = visitor;
            _navigationItem = navigationItem;
        }

        private void AddKey(ParameterExpression parameter)
        {
            var edmEnityType = (IEdmEntityType)_visitor.EdmModel.FindType(parameter.Type.FullName);
            foreach (IEdmStructuralProperty keyProperty in edmEnityType.DeclaredKey)
            {
                PropertyInfo property = parameter.Type.GetProperty(keyProperty.Name);
                _navigationItem.AddSelectItem(new OeSelectItem(keyProperty, Expression.MakeMemberAccess(parameter, property), false));
            }
        }
        private static bool HasSelectItems(SelectExpandClause selectExpandClause)
        {
            if (selectExpandClause != null)
                foreach (SelectItem odataSelectItem in selectExpandClause.SelectedItems)
                    if (odataSelectItem is PathSelectItem pathSelectItem && pathSelectItem.SelectedPath.LastSegment is PropertySegment)
                        return true;

            return false;
        }
        public Expression Build(Expression source, OeQueryContext queryContext)
        {
            if (queryContext.ODataUri.OrderBy != null && queryContext.PageSize > 0)
            {
                bool hasSelectItems = HasSelectItems(queryContext.ODataUri.SelectAndExpand);
                source = BuildOrderBySkipToken(queryContext.ODataUri.OrderBy, source, queryContext.GroupJoinExpressionBuilder, hasSelectItems);
                var expressionBuilder = new OeExpressionBuilder(_visitor, queryContext.GroupJoinExpressionBuilder);
                source = expressionBuilder.ApplySkipToken(source, queryContext.SkipTokenNameValues, queryContext.ODataUri.OrderBy, queryContext.IsDatabaseNullHighestValue);
                source = expressionBuilder.ApplyOrderBy(source, queryContext.ODataUri.OrderBy);
                source = expressionBuilder.ApplySkip(source, queryContext.ODataUri.Skip, queryContext.ODataUri.Path);
                source = expressionBuilder.ApplyTake(source, queryContext.ODataUri.Top, queryContext.ODataUri.Path);
            }

            if (queryContext.ODataUri.SelectAndExpand != null)
                source = BuildSelect(queryContext.ODataUri.SelectAndExpand, source, _visitor.Parameter,
                    queryContext.MetadataLevel, queryContext.NavigationNextLink, queryContext.GroupJoinExpressionBuilder, false);

            if (queryContext.ODataUri.Compute != null)
                BuildCompute(queryContext.ODataUri.Compute);

            source = CreateSelectExpression(source, _visitor.Parameter);
            _entryFactory = CreateEntryFactory(_navigationItem, source);
            return source;
        }
        private void BuildCompute(ComputeClause computeClause)
        {
            foreach (ComputeExpression computeExpression in computeClause.ComputedItems)
            {
                Expression expression = _visitor.TranslateNode(computeExpression.Expression);
                IEdmTypeReference edmTypeReference = OeEdmClrHelper.GetEdmTypeReference(_visitor.EdmModel, expression.Type);
                _navigationItem.AddSelectItem(new OeSelectItem(new ComputeProperty(computeExpression.Alias, edmTypeReference), expression, false));
            }
        }
        private Expression BuildOrderBySkipToken(OrderByClause orderByClause, Expression source, OeGroupJoinExpressionBuilder groupJoinBuilder, bool hasSelectItems)
        {
            while (orderByClause != null)
            {
                var propertyNode = (SingleValuePropertyAccessNode)orderByClause.Expression;
                if (propertyNode.Source is SingleNavigationNode navigationNode)
                {
                    OeSelectItem match = null;
                    ExpandedNavigationSelectItem navigationSelectItem = null;
                    do
                    {
                        if ((match = _navigationItem.FindHierarchyNavigationItem(navigationNode.NavigationProperty)) != null)
                            break;

                        SelectExpandClause selectExpandClause;
                        if (navigationSelectItem == null)
                        {
                            var pathSelectItem = new PathSelectItem(new ODataSelectPath(new PropertySegment((IEdmStructuralProperty)propertyNode.Property)));
                            selectExpandClause = new SelectExpandClause(new[] { pathSelectItem }, false);
                        }
                        else
                            selectExpandClause = new SelectExpandClause(new[] { navigationSelectItem }, false);

                        var segment = new NavigationPropertySegment(navigationNode.NavigationProperty, navigationNode.NavigationSource);
                        navigationSelectItem = new ExpandedNavigationSelectItem(new ODataExpandPath(segment), navigationNode.NavigationSource, selectExpandClause);
                    }
                    while ((navigationNode = navigationNode.Source as SingleNavigationNode) != null);

                    if (navigationSelectItem != null)
                    {
                        if (match == null)
                            match = _navigationItem;

                        Type clrType = OeEdmClrHelper.GetClrType(_visitor.EdmModel, match.EntitySet.Type);
                        var selectItemTranslator = new OeSelectItemTranslator(match, _visitor, match.Path, OeMetadataLevel.None, false, Expression.Parameter(clrType), source, groupJoinBuilder, true);
                        navigationSelectItem.TranslateWith(selectItemTranslator);
                        source = selectItemTranslator.Source;
                    }
                }
                else
                {
                    if (hasSelectItems)
                    {
                        Expression e = _visitor.TranslateNode(orderByClause.Expression);
                        _navigationItem.AddSelectItem(new OeSelectItem(null, e, true));
                    }
                }

                orderByClause = orderByClause.ThenBy;
            }

            return source;
        }
        public Expression BuildSelect(SelectExpandClause selectClause, Expression source, ParameterExpression parameter,
            OeMetadataLevel metadataLevel, bool navigationNextLink, OeGroupJoinExpressionBuilder groupJoinBuilder, bool skipToken)
        {
            foreach (SelectItem selectItemClause in selectClause.SelectedItems)
            {
                var selectItemTranslator = new OeSelectItemTranslator(_navigationItem, _visitor, _navigationItem.Path, metadataLevel, navigationNextLink, parameter, source, groupJoinBuilder, skipToken);
                OeSelectItem selectItem = selectItemClause.TranslateWith(selectItemTranslator);
                if (selectItem == null)
                    continue;

                if (selectItem.EdmProperty is IEdmNavigationProperty)
                    source = selectItemTranslator.Source;
                else
                    _navigationItem.AddSelectItem(selectItem);
            }

            if (_navigationItem.HasSelectItems && metadataLevel == OeMetadataLevel.Full)
                AddKey(parameter);

            return source;
        }
        private static OeEntryFactory CreateEntryFactory(OeSelectItem root, Expression source)
        {
            ParameterExpression parameter = Expression.Parameter(typeof(Object));
            UnaryExpression typedParameter = Expression.Convert(parameter, OeExpressionHelper.GetCollectionItemType(source.Type));

            if (root.HasNavigationItems)
            {
                List<OeSelectItem> navigationItems = FlattenNavigationItems(root);
                IReadOnlyList<MemberExpression> navigationProperties = OeExpressionHelper.GetPropertyExpressions(typedParameter);
                for (int i = navigationItems.Count - 1; i >= 0; i--)
                {
                    OeSelectItem navigationItem = navigationItems[i];
                    OeEntryFactory[] nestedNavigationLinks = GetNestedNavigationLinks(navigationItem);

                    OeEntryFactory entryFactory;
                    OePropertyAccessor[] accessors = GetAccessors(navigationProperties[i].Type, navigationItem.EntitySet, navigationItem.SelectItems);
                    if (i == 0)
                        entryFactory = OeEntryFactory.CreateEntryFactoryParent(navigationItem.EntitySet, accessors, nestedNavigationLinks);
                    else
                        entryFactory = OeEntryFactory.CreateEntryFactoryNested(navigationItem.EntitySet, accessors, navigationItem.Resource, nestedNavigationLinks);

                    entryFactory.LinkAccessor = (Func<Object, Object>)Expression.Lambda(navigationProperties[i], parameter).Compile();
                    entryFactory.CountOption = navigationItem.CountOption;
                    navigationItem.EntryFactory = entryFactory;
                }
            }
            else
            {
                IReadOnlyList<MemberExpression> propertyExpressions = OeExpressionHelper.GetPropertyExpressions(typedParameter);
                OePropertyAccessor[] accessors;
                if (root.SelectItems.Count == 0)
                    accessors = OePropertyAccessor.CreateFromType(typedParameter.Type, root.EntitySet);
                else
                {
                    var accessorList = new List<OePropertyAccessor>(root.SelectItems.Count);
                    for (int i = 0; i < root.SelectItems.Count; i++)
                        if (root.SelectItems[i].EdmProperty != null)
                            accessorList.Add(OePropertyAccessor.CreatePropertyAccessor(root.SelectItems[i].EdmProperty, propertyExpressions[i], parameter));
                    accessors = accessorList.ToArray();
                }
                root.EntryFactory = OeEntryFactory.CreateEntryFactory(root.EntitySet, accessors);
            }

            return root.EntryFactory;
        }
        public OeEntryFactory CreateEntryFactory(Type clrEntityType, IEdmEntitySet entitySet, Type sourceType)
        {
            return _entryFactory;
        }
        private MethodCallExpression CreateSelectExpression(Expression source, ParameterExpression parameter)
        {
            if (_navigationItem.HasNavigationItems)
                return (MethodCallExpression)source;

            if (_navigationItem.SelectItems.Count == 0)
                return (MethodCallExpression)source;

            var expressions = new List<Expression>(_navigationItem.SelectItems.Count);
            for (int i = 0; i < _navigationItem.SelectItems.Count; i++)
                expressions.Add(_navigationItem.SelectItems[i].Expression);
            NewExpression newTupleExpression = OeExpressionHelper.CreateTupleExpression(expressions);

            LambdaExpression lambda = Expression.Lambda(newTupleExpression, parameter);
            MethodInfo selectMethodInfo = OeMethodInfoHelper.GetSelectMethodInfo(parameter.Type, newTupleExpression.Type);
            return Expression.Call(selectMethodInfo, source, lambda);
        }
        private static List<OeSelectItem> FlattenNavigationItems(OeSelectItem root)
        {
            var navigationItems = new List<OeSelectItem>();
            var stack = new Stack<ValueTuple<OeSelectItem, int>>();
            stack.Push(new ValueTuple<OeSelectItem, int>(root, 0));
            do
            {
                ValueTuple<OeSelectItem, int> stackItem = stack.Pop();
                if (stackItem.Item2 == 0 && !stackItem.Item1.SkipToken)
                    navigationItems.Add(stackItem.Item1);

                if (stackItem.Item2 < stackItem.Item1.NavigationItems.Count)
                {
                    stack.Push(new ValueTuple<OeSelectItem, int>(stackItem.Item1, stackItem.Item2 + 1));
                    OeSelectItem selectItem = stackItem.Item1.NavigationItems[stackItem.Item2];
                    stack.Push(new ValueTuple<OeSelectItem, int>(selectItem, 0));
                }
            }
            while (stack.Count > 0);
            return navigationItems;
        }
        private static OePropertyAccessor[] GetAccessors(Type clrEntityType, IEdmEntitySet entitySet, IReadOnlyList<OeSelectItem> selectItems)
        {
            if (selectItems.Count == 0)
                return OePropertyAccessor.CreateFromType(clrEntityType, entitySet);

            var accessorList = new List<OePropertyAccessor>(selectItems.Count);

            ParameterExpression accessorParameter = Expression.Parameter(typeof(Object));
            UnaryExpression typedAccessorParameter = Expression.Convert(accessorParameter, clrEntityType);
            for (int i = 0; i < selectItems.Count; i++)
                if (!selectItems[i].SkipToken && selectItems[i].EdmProperty is IEdmStructuralProperty)
                {
                    PropertyInfo clrProperty = clrEntityType.GetPropertyIgnoreCase(selectItems[i].EdmProperty.Name);
                    MemberExpression accessorExpression = Expression.Property(typedAccessorParameter, clrProperty);
                    accessorList.Add(OePropertyAccessor.CreatePropertyAccessor(selectItems[i].EdmProperty, accessorExpression, accessorParameter));
                }

            return accessorList.ToArray();
        }
        private static OeEntryFactory[] GetNestedNavigationLinks(OeSelectItem navigationItem)
        {
            var nestedEntryFactories = new List<OeEntryFactory>(navigationItem.NavigationItems.Count);
            for (int i = 0; i < navigationItem.NavigationItems.Count; i++)
                if (!navigationItem.NavigationItems[i].SkipToken)
                    nestedEntryFactories.Add(navigationItem.NavigationItems[i].EntryFactory);
            return nestedEntryFactories.ToArray();
        }
        private static IEdmEntitySet GetEntitySet(ODataPath path)
        {
            if (path.LastSegment is EntitySetSegment entitySetSegment)
                return entitySetSegment.EntitySet;

            if (path.LastSegment is NavigationPropertySegment navigationPropertySegment)
                return (IEdmEntitySet)navigationPropertySegment.NavigationSource;

            if (path.LastSegment is KeySegment keySegment)
                return (IEdmEntitySet)keySegment.NavigationSource;

            throw new InvalidOperationException("unknown segment type " + path.LastSegment.ToString());
        }
    }
}
