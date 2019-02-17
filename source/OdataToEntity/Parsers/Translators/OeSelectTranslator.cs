using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace OdataToEntity.Parsers.Translators
{
    public readonly struct OeSelectTranslator
    {
        private sealed class ComputeProperty : IEdmProperty
        {
            public ComputeProperty(String alias, IEdmTypeReference edmTypeReference, Expression expression)
            {
                Name = alias;
                Type = edmTypeReference;
                Expression = expression;
            }

            public IEdmStructuredType DeclaringType => ModelBuilder.PrimitiveTypeHelper.TupleEdmType;
            public Expression Expression { get; }
            public String Name { get; }
            public EdmPropertyKind PropertyKind => throw new NotSupportedException();
            public IEdmTypeReference Type { get; }
        }

        private sealed class ReplaceParameterVisitor : ExpressionVisitor
        {
            private readonly Expression _source;

            public ReplaceParameterVisitor(Expression source)
            {
                _source = source;
            }

            protected override Expression VisitParameter(ParameterExpression node)
            {
                return node.Type == _source.Type ? _source : base.VisitParameter(node);
            }
        }

        private readonly OeJoinBuilder _joinBuilder;
        private readonly OeMetadataLevel _metadataLevel;
        private readonly OeSelectItem _navigationItem;
        private readonly OeQueryNodeVisitor _visitor;

        public OeSelectTranslator(OeJoinBuilder joinBuilder, ODataPath path, OeMetadataLevel metadataLevel)
        {
            _joinBuilder = joinBuilder;
            _metadataLevel = metadataLevel;
            _navigationItem = new OeSelectItem(path);
            _visitor = joinBuilder.Visitor;
        }

        public Expression Build(Expression source, OeQueryContext queryContext)
        {
            bool isBuild = false;
            if (queryContext.ODataUri.Skip != null || queryContext.ODataUri.Top != null)
            {
                isBuild = true;
                source = BuildSkipTakeSource(source, queryContext, _navigationItem);
            }

            if (queryContext.UseModelBoundAttribute == OeModelBoundAttribute.Yes || queryContext.ODataUri.SelectAndExpand != null)
            {
                BuildSelect(queryContext.ODataUri.SelectAndExpand, queryContext.NavigationNextLink, queryContext.UseModelBoundAttribute);
                isBuild |= _navigationItem.SelectItems.Count > 0 || _navigationItem.NavigationItems.Count > 0;
            }

            if (queryContext.ODataUri.Compute != null)
            {
                isBuild = true;
                BuildCompute(queryContext.ODataUri.Compute);
            }

            source = BuildJoin(source, queryContext.UseModelBoundAttribute);
            source = BuildOrderBy(source, queryContext.ODataUri.OrderBy);

            if (isBuild)
            {
                source = SelectStructuralProperties(source, _navigationItem);
                source = CreateSelectExpression(source, _joinBuilder);
            }

            return source;
        }
        private void BuildCompute(ComputeClause computeClause)
        {
            foreach (ComputeExpression computeExpression in computeClause.ComputedItems)
            {
                Expression expression = _visitor.TranslateNode(computeExpression.Expression);
                IEdmTypeReference edmTypeReference = OeEdmClrHelper.GetEdmTypeReference(_visitor.EdmModel, expression.Type);
                _navigationItem.AddSelectItem(new OeSelectItem(new ComputeProperty(computeExpression.Alias, edmTypeReference, expression), false));
            }
        }
        private Expression BuildJoin(Expression source, OeModelBoundAttribute useModelBoundAttribute)
        {
            List<OeSelectItem> navigationItems = FlattenNavigationItems(_navigationItem, true);
            for (int i = 1; i < navigationItems.Count; i++)
                if (!navigationItems[i].AlreadyUsedInBuildExpression)
                {
                    navigationItems[i].AlreadyUsedInBuildExpression = true;

                    ODataPathSegment segment = navigationItems[i].ExpandedNavigationSelectItem.PathToNavigationProperty.LastSegment;
                    IEdmNavigationProperty navigationProperty = (((NavigationPropertySegment)segment).NavigationProperty);
                    Expression innerSource = GetInnerSource(navigationItems[i], useModelBoundAttribute);
                    source = _joinBuilder.Build(source, innerSource, navigationItems[i].Parent.GetJoinPath(), navigationProperty);
                }

            _visitor.ChangeParameterType(source);
            return source;
        }
        private Expression BuildOrderBy(Expression source, OrderByClause orderByClause)
        {
            _joinBuilder.Visitor.ChangeParameterType(source);
            if (!(source is MethodCallExpression callExpression &&
                (callExpression.Method.Name == nameof(Enumerable.Skip) || callExpression.Method.Name == nameof(Enumerable.Take))))
                source = OeOrderByTranslator.Build(_joinBuilder, source, _joinBuilder.Visitor.Parameter, orderByClause);

            List<OeSelectItem> navigationItems = FlattenNavigationItems(_navigationItem, false);
            for (int i = 1; i < navigationItems.Count; i++)
            {
                ExpandedNavigationSelectItem item = navigationItems[i].ExpandedNavigationSelectItem;
                if (item.OrderByOption != null && item.TopOption == null && item.SkipOption == null)
                {
                    IReadOnlyList<IEdmNavigationProperty> joinPath = navigationItems[i].GetJoinPath();
                    source = OeOrderByTranslator.BuildNested(_joinBuilder, source, _joinBuilder.Visitor.Parameter, item.OrderByOption, joinPath);
                }
            }

            return source;
        }
        private void BuildOrderBySkipTake(OeSelectItem navigationItem, OrderByClause orderByClause, bool hasSelectItems)
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
                        if ((match = navigationItem.FindHierarchyNavigationItem(navigationNode.NavigationProperty)) != null)
                        {
                            match.AddSelectItem(new OeSelectItem(propertyNode.Property, true));
                            break;
                        }

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
                            match = navigationItem;

                        var selectItemTranslator = new OeSelectItemTranslator(_joinBuilder, false, true);
                        selectItemTranslator.Translate(match, navigationSelectItem);
                    }
                }
                else
                {
                    if (hasSelectItems)
                        navigationItem.AddSelectItem(new OeSelectItem(propertyNode.Property, true));
                }

                orderByClause = orderByClause.ThenBy;
            }
        }
        public void BuildSelect(SelectExpandClause selectClause, bool navigationNextLink, OeModelBoundAttribute useModelBoundAttribute)
        {
            if (selectClause != null)
            {
                var selectItemTranslator = new OeSelectItemTranslator(_joinBuilder, navigationNextLink, false);
                foreach (SelectItem selectItemClause in selectClause.SelectedItems)
                    selectItemTranslator.Translate(_navigationItem, selectItemClause);
            }

            if (useModelBoundAttribute == OeModelBoundAttribute.Yes)
            {
                SelectItem[] selectItems = _visitor.EdmModel.GetSelectItems(_navigationItem.EntitySet.EntityType());
                if (selectItems != null)
                {
                    var selectItemTranslator = new OeSelectItemTranslator(_joinBuilder, navigationNextLink, false);
                    foreach (SelectItem selectItemClause in selectItems)
                        selectItemTranslator.Translate(_navigationItem, selectItemClause);
                }
            }

            _navigationItem.AddKeyRecursive(_metadataLevel != OeMetadataLevel.Full);
        }
        private Expression BuildSkipTakeSource(Expression source, OeQueryContext queryContext, OeSelectItem navigationItem)
        {
            long? top = queryContext.ODataUri.Top;
            if (queryContext.UseModelBoundAttribute == OeModelBoundAttribute.Yes)
            {
                Query.PageAttribute pageAttribute = _visitor.EdmModel.GetPageAttribute(navigationItem.EntitySet.EntityType());
                if (pageAttribute != null)
                {
                    navigationItem.MaxTop = pageAttribute.MaxTop;
                    navigationItem.PageSize = pageAttribute.PageSize;
                }

                if (navigationItem.MaxTop > 0 && navigationItem.MaxTop < top.GetValueOrDefault())
                    top = navigationItem.MaxTop;

                if (navigationItem.PageSize > 0 && navigationItem.PageSize < top.GetValueOrDefault())
                    top = navigationItem.PageSize;
            }

            bool hasSelectItems = HasSelectItems(queryContext.ODataUri.SelectAndExpand);
            BuildOrderBySkipTake(navigationItem, queryContext.ODataUri.OrderBy, hasSelectItems);
            source = BuildJoin(source, queryContext.UseModelBoundAttribute);

            var expressionBuilder = new OeExpressionBuilder(queryContext.JoinBuilder);
            source = expressionBuilder.ApplySkipToken(source, queryContext.SkipTokenNameValues, queryContext.ODataUri.OrderBy, queryContext.IsDatabaseNullHighestValue);
            source = expressionBuilder.ApplyOrderBy(source, queryContext.ODataUri.OrderBy);
            source = expressionBuilder.ApplySkip(source, queryContext.ODataUri.Skip, queryContext.ODataUri.Path);
            return expressionBuilder.ApplyTake(source, top, queryContext.ODataUri.Path);
        }
        public OeEntryFactory CreateEntryFactory(IEdmEntitySet entitySet, Type clrType)
        {
            return CreateEntryFactory(_visitor.EdmModel, _navigationItem, clrType);
        }
        private static OeEntryFactory CreateEntryFactory(IEdmModel edmModel, OeSelectItem root, Type clrType)
        {
            ParameterExpression parameter = Expression.Parameter(typeof(Object));
            UnaryExpression typedParameter = Expression.Convert(parameter, clrType);

            if (root.NavigationItems.Count > 0)
            {
                List<OeSelectItem> navigationItems = FlattenNavigationItems(root, false);
                IReadOnlyList<MemberExpression> navigationProperties = OeExpressionHelper.GetPropertyExpressions(typedParameter);

                for (int i = navigationItems.Count - 1; i >= 0; i--)
                {
                    OeSelectItem navigationItem = navigationItems[i];
                    OeEntryFactory[] nestedNavigationLinks = GetNestedNavigationLinks(navigationItem);

                    OeEntryFactory entryFactory;
                    Type clrEntityType = edmModel.GetClrType(navigationItem.EntitySet);
                    OePropertyAccessor[] accessors = GetAccessors(navigationProperties[i].Type, navigationItem.EntitySet, navigationItem.SelectItems);
                    LambdaExpression linkAccessor = Expression.Lambda(navigationProperties[i], parameter);
                    if (i == 0)
                        entryFactory = OeEntryFactory.CreateEntryFactoryParent(clrEntityType, navigationItem.EntitySet, accessors, nestedNavigationLinks, linkAccessor);
                    else
                    {
                        var resourceInfo = new ODataNestedResourceInfo()
                        {
                            IsCollection = navigationItem.EdmProperty.Type.Definition is EdmCollectionType,
                            Name = navigationItem.EdmProperty.Name
                        };
                        entryFactory = OeEntryFactory.CreateEntryFactoryNested(clrEntityType, navigationItem.EntitySet, (IEdmNavigationProperty)navigationItem.EdmProperty,
                            accessors, nestedNavigationLinks, linkAccessor, resourceInfo);
                        entryFactory.CountOption = navigationItem.ExpandedNavigationSelectItem.CountOption;
                    }

                    entryFactory.MaxTop = navigationItem.MaxTop;
                    entryFactory.PageSize = navigationItem.PageSize;
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
                    {
                        OeSelectItem selectItem = root.SelectItems[i];
                        accessorList.Add(OePropertyAccessor.CreatePropertyAccessor(selectItem.EdmProperty, propertyExpressions[i], parameter, selectItem.SkipToken));
                    }
                    accessors = accessorList.ToArray();
                }
                Type clrEntityType = edmModel.GetClrType(root.EntitySet);

                root.EntryFactory = OeEntryFactory.CreateEntryFactory(clrEntityType, root.EntitySet, accessors);
                root.EntryFactory.MaxTop = root.MaxTop;
                root.EntryFactory.PageSize = root.PageSize;
            }

            return root.EntryFactory;
        }
        private MethodCallExpression CreateSelectExpression(Expression source, OeJoinBuilder joinBuilder)
        {
            if (_navigationItem.NavigationItems.Count > 0)
                return (MethodCallExpression)source;

            if (_navigationItem.SelectItems.Count == 0)
                return (MethodCallExpression)source;

            var expressions = new List<Expression>(_navigationItem.SelectItems.Count);
            for (int i = 0; i < _navigationItem.SelectItems.Count; i++)
            {
                IEdmProperty edmProperty = _navigationItem.SelectItems[i].EdmProperty;
                PropertyInfo clrProperty = _visitor.Parameter.Type.GetPropertyIgnoreCase(edmProperty);
                expressions.Add(Expression.MakeMemberAccess(_visitor.Parameter, clrProperty));
            }
            NewExpression newTupleExpression = OeExpressionHelper.CreateTupleExpression(expressions);

            LambdaExpression lambda = Expression.Lambda(newTupleExpression, _visitor.Parameter);
            MethodInfo selectMethodInfo = OeMethodInfoHelper.GetSelectMethodInfo(_visitor.Parameter.Type, newTupleExpression.Type);
            return Expression.Call(selectMethodInfo, source, lambda);
        }
        private static List<OeSelectItem> FlattenNavigationItems(OeSelectItem root, bool includeSkipToken)
        {
            var navigationItems = new List<OeSelectItem>();
            var stack = new Stack<ValueTuple<OeSelectItem, int>>();
            stack.Push(new ValueTuple<OeSelectItem, int>(root, 0));
            do
            {
                ValueTuple<OeSelectItem, int> stackItem = stack.Pop();
                if (stackItem.Item2 == 0 && (!stackItem.Item1.SkipToken || includeSkipToken))
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
        private static OePropertyAccessor[] GetAccessors(Type clrEntityType, IEdmEntitySetBase entitySet, IReadOnlyList<OeSelectItem> selectItems)
        {
            if (selectItems.Count == 0)
                return OePropertyAccessor.CreateFromType(clrEntityType, entitySet);

            var accessors = new OePropertyAccessor[selectItems.Count];

            ParameterExpression parameter = Expression.Parameter(typeof(Object));
            UnaryExpression typedAccessorParameter = Expression.Convert(parameter, clrEntityType);
            IReadOnlyList<MemberExpression> propertyExpressions = OeExpressionHelper.GetPropertyExpressions(typedAccessorParameter);
            for (int i = 0; i < selectItems.Count; i++)
                accessors[i] = OePropertyAccessor.CreatePropertyAccessor(selectItems[i].EdmProperty, propertyExpressions[i], parameter, selectItems[i].SkipToken);

            return accessors;
        }
        private Expression GetInnerSource(OeSelectItem navigationItem, OeModelBoundAttribute useModelBoundAttribute)
        {
            IEdmModel edmModel = _joinBuilder.Visitor.EdmModel;

            Type clrEntityType = edmModel.GetClrType(navigationItem.EdmProperty.DeclaringType);
            PropertyInfo navigationClrProperty = clrEntityType.GetPropertyIgnoreCase(navigationItem.EdmProperty);

            Type itemType = OeExpressionHelper.GetCollectionItemType(navigationClrProperty.PropertyType);
            if (itemType == null)
                itemType = navigationClrProperty.PropertyType;

            var visitor = new OeQueryNodeVisitor(_joinBuilder.Visitor, Expression.Parameter(itemType));
            var expressionBuilder = new OeExpressionBuilder(_joinBuilder, visitor);

            var navigationProperty = (IEdmNavigationProperty)navigationItem.EdmProperty;
            if (navigationProperty.ContainsTarget)
            {
                ModelBuilder.ManyToManyJoinDescription joinDescription = edmModel.GetManyToManyJoinDescription(navigationProperty);
                navigationProperty = joinDescription.TargetNavigationProperty;
            }
            IEdmEntitySet innerEntitySet = OeEdmClrHelper.GetEntitySet(edmModel, navigationProperty);
            Expression innerSource = OeEnumerableStub.CreateEnumerableStubExpression(itemType, innerEntitySet);

            ExpandedNavigationSelectItem item = navigationItem.ExpandedNavigationSelectItem;
            innerSource = expressionBuilder.ApplyFilter(innerSource, item.FilterOption);
            if (useModelBoundAttribute == OeModelBoundAttribute.Yes || item.SkipOption != null || item.TopOption != null)
            {
                long? top = item.TopOption;
                if (useModelBoundAttribute == OeModelBoundAttribute.Yes)
                {
                    Query.PageAttribute pageAttribute = _joinBuilder.Visitor.EdmModel.GetPageAttribute((IEdmNavigationProperty)navigationItem.EdmProperty);
                    if (pageAttribute != null)
                    {
                        navigationItem.MaxTop = pageAttribute.MaxTop;
                        navigationItem.PageSize = pageAttribute.PageSize;
                    }

                    if (navigationItem.MaxTop > 0 && navigationItem.MaxTop < top.GetValueOrDefault())
                        top = navigationItem.MaxTop;

                    if (navigationItem.PageSize > 0 && (top == null || navigationItem.PageSize < top.GetValueOrDefault()))
                        top = navigationItem.PageSize;
                }

                OrderByClause orderByClause = item.OrderByOption;
                if (navigationItem.PageSize > 0)
                    orderByClause = OeSkipTokenParser.GetUniqueOrderBy(_visitor.EdmModel, navigationItem.EntitySet, item.OrderByOption, null);

                var entitySet = (IEdmEntitySet)navigationItem.Parent.EntitySet;
                Expression source = OeEnumerableStub.CreateEnumerableStubExpression(navigationClrProperty.DeclaringType, entitySet);
                innerSource = OeCrossApplyBuilder.Build(expressionBuilder, source, innerSource, navigationItem.Path, orderByClause, item.SkipOption, top);
            }

            return innerSource;
        }
        private static OeEntryFactory[] GetNestedNavigationLinks(OeSelectItem navigationItem)
        {
            var nestedEntryFactories = new List<OeEntryFactory>(navigationItem.NavigationItems.Count);
            for (int i = 0; i < navigationItem.NavigationItems.Count; i++)
                if (!navigationItem.NavigationItems[i].SkipToken)
                    nestedEntryFactories.Add(navigationItem.NavigationItems[i].EntryFactory);
            return nestedEntryFactories.ToArray();
        }
        private static bool HasSelectItems(SelectExpandClause selectExpandClause)
        {
            if (selectExpandClause != null)
                foreach (SelectItem odataSelectItem in selectExpandClause.SelectedItems)
                    if (odataSelectItem is PathSelectItem pathSelectItem && pathSelectItem.SelectedPath.LastSegment is PropertySegment)
                        return true;

            return false;
        }
        private static Expression SelectStructuralProperties(Expression source, OeSelectItem root)
        {
            if (root.NavigationItems.Count == 0)
                return source;

            ParameterExpression parameter = Expression.Parameter(OeExpressionHelper.GetCollectionItemType(source.Type));
            IReadOnlyList<MemberExpression> joins = OeExpressionHelper.GetPropertyExpressions(parameter);
            var newJoins = new Expression[joins.Count];

            List<OeSelectItem> navigationItems = FlattenNavigationItems(root, true);
            for (int i = 0; i < navigationItems.Count; i++)
            {
                newJoins[i] = joins[i];
                if (navigationItems[i].SelectItems.Count > 0)
                {
                    var properties = new Expression[navigationItems[i].SelectItems.Count];
                    for (int j = 0; j < navigationItems[i].SelectItems.Count; j++)
                        if (navigationItems[i].SelectItems[j].EdmProperty is ComputeProperty computeProperty)
                            properties[j] = new ReplaceParameterVisitor(joins[i]).Visit(computeProperty.Expression);
                        else
                        {
                            PropertyInfo property = joins[i].Type.GetPropertyIgnoreCase(navigationItems[i].SelectItems[j].EdmProperty);
                            properties[j] = Expression.Property(joins[i], property);
                        }
                    Expression newTupleExpression = OeExpressionHelper.CreateTupleExpression(properties);

                    if (i > 0 && navigationItems[i].EdmProperty.Type.IsNullable)
                    {
                        UnaryExpression nullConstant = Expression.Convert(OeConstantToVariableVisitor.NullConstantExpression, newTupleExpression.Type);
                        newTupleExpression = Expression.Condition(Expression.Equal(joins[i], OeConstantToVariableVisitor.NullConstantExpression), nullConstant, newTupleExpression);
                    }
                    newJoins[i] = newTupleExpression;
                }
            }

            NewExpression newSelectorBody = OeExpressionHelper.CreateTupleExpression(newJoins);
            MethodInfo selectMethodInfo = OeMethodInfoHelper.GetSelectMethodInfo(parameter.Type, newSelectorBody.Type);
            LambdaExpression newSelector = Expression.Lambda(newSelectorBody, parameter);

            //Quirk EF Core 2.1.1 bug Take/Skip must be last in expression tree
            var skipTakeExpressions = new List<MethodCallExpression>();
            while (source is MethodCallExpression callExpression && (callExpression.Method.Name == nameof(Enumerable.Skip) || callExpression.Method.Name == nameof(Enumerable.Take)))
            {
                skipTakeExpressions.Add(callExpression);
                source = callExpression.Arguments[0];
            }

            source = Expression.Call(selectMethodInfo, source, newSelector);

            for (int i = skipTakeExpressions.Count - 1; i >= 0; i--)
            {
                MethodInfo skipTakeMethodInfo = skipTakeExpressions[i].Method.GetGenericMethodDefinition().MakeGenericMethod(newSelector.ReturnType);
                source = Expression.Call(skipTakeMethodInfo, source, skipTakeExpressions[i].Arguments[1]);
            }

            return source;
        }
    }
}
