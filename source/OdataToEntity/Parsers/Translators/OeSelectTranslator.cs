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
        private sealed class ComputeProperty : IEdmStructuralProperty
        {
            public ComputeProperty(String alias, IEdmTypeReference edmTypeReference, Expression expression)
            {
                Name = alias;
                Type = edmTypeReference;
                Expression = expression;
            }

            public IEdmStructuredType DeclaringType => ModelBuilder.PrimitiveTypeHelper.TupleEdmType;
            public String DefaultValueString => throw new NotSupportedException();
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

        private readonly IEdmModel _edmModel;
        private readonly OeJoinBuilder _joinBuilder;
        private readonly ODataUri _odataUri;
        private readonly OeNavigationSelectItem _rootNavigationItem;

        public OeSelectTranslator(IEdmModel edmModel, OeJoinBuilder joinBuilder, ODataUri odataUri)
        {
            _edmModel = edmModel;
            _joinBuilder = joinBuilder;
            _odataUri = odataUri;
            _rootNavigationItem = new OeNavigationSelectItem(odataUri);
        }

        public Expression Build(Expression source, ref OeSelectTranslatorParameters parameters, out OeEntryFactoryFactory entryFactoryFactory)
        {
            entryFactoryFactory = new OeSelectEntryFactoryFactory(_rootNavigationItem);

            BuildSelect(_odataUri.SelectAndExpand, parameters.MetadataLevel);
            BuildCompute(_odataUri.Compute);

            source = BuildSkipTakeSource(source, parameters.SkipTokenNameValues, parameters.IsDatabaseNullHighestValue);
            source = BuildJoin(source, FlattenNavigationItems(_rootNavigationItem, false));
            source = BuildOrderBy(source, _odataUri.OrderBy);
            source = SelectStructuralProperties(source, _rootNavigationItem);
            return CreateSelectExpression(source);
        }
        private void BuildCompute(ComputeClause computeClause)
        {
            if (computeClause != null)
            {
                var selectNullableVisitor = new SelectNullableVisitor();
                foreach (ComputeExpression computeExpression in computeClause.ComputedItems)
                {
                    Expression expression = _joinBuilder.Visitor.TranslateNode(computeExpression.Expression);
                    expression = selectNullableVisitor.Visit(expression);
                    IEdmTypeReference edmTypeReference = OeEdmClrHelper.GetEdmTypeReference(_edmModel, expression.Type);
                    var computeProperty = new ComputeProperty(computeExpression.Alias, edmTypeReference, expression);
                    _rootNavigationItem.AddStructuralItem(computeProperty, false);
                }
            }
        }
        private Expression BuildJoin(Expression source, IList<OeNavigationSelectItem> navigationItems)
        {
            for (int i = 1; i < navigationItems.Count; i++)
            {
                OeNavigationSelectItem navigationItem = navigationItems[i];
                if (navigationItem.Parent == null)
                    throw new InvalidOperationException("Parent is null must be first element in " + nameof(navigationItems));

                if (!navigationItem.AlreadyUsedInBuildExpression)
                {
                    navigationItem.AlreadyUsedInBuildExpression = true;

                    ODataPathSegment segment = navigationItem.NavigationSelectItem.PathToNavigationProperty.LastSegment;
                    IEdmNavigationProperty navigationProperty = (((NavigationPropertySegment)segment).NavigationProperty);
                    Expression innerSource = GetInnerSource(navigationItem);
                    source = _joinBuilder.Build(_edmModel, source, innerSource, navigationItem.Parent.GetJoinPath(), navigationProperty);
                }
            }

            _joinBuilder.Visitor.ChangeParameterType(source);
            return source;
        }
        private Expression BuildOrderBy(Expression source, OrderByClause orderByClause)
        {
            _joinBuilder.Visitor.ChangeParameterType(source);
            if (!(source is MethodCallExpression callExpression &&
                (callExpression.Method.Name == nameof(Enumerable.Skip) || callExpression.Method.Name == nameof(Enumerable.Take))))
                source = OeOrderByTranslator.Build(_joinBuilder, source, _joinBuilder.Visitor.Parameter, orderByClause);

            List<OeNavigationSelectItem> navigationItems = FlattenNavigationItems(_rootNavigationItem, false);
            for (int i = 1; i < navigationItems.Count; i++)
            {
                ExpandedNavigationSelectItem item = navigationItems[i].NavigationSelectItem;
                if (item.OrderByOption != null && item.TopOption == null && item.SkipOption == null)
                {
                    IReadOnlyList<IEdmNavigationProperty> joinPath = navigationItems[i].GetJoinPath();
                    source = OeOrderByTranslator.BuildNested(_joinBuilder, source, _joinBuilder.Visitor.Parameter, item.OrderByOption, joinPath);
                }
            }

            return source;
        }
        private void BuildOrderBySkipTake(OeNavigationSelectItem navigationItem, OrderByClause orderByClause, bool hasSelectItems)
        {
            while (orderByClause != null)
            {
                var propertyNode = (SingleValuePropertyAccessNode)orderByClause.Expression;
                if (propertyNode.Source is SingleNavigationNode navigationNode)
                {
                    OeNavigationSelectItem? match;
                    ExpandedNavigationSelectItem? navigationSelectItem = null;
                    for (; ; )
                    {
                        if ((match = navigationItem.FindHierarchyNavigationItem(navigationNode.NavigationProperty)) != null)
                        {
                            match.AddStructuralItem((IEdmStructuralProperty)propertyNode.Property, true);
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

                        if (navigationNode.Source is SingleNavigationNode singleNavigationNode)
                            navigationNode = singleNavigationNode;
                        else
                            break;
                    }

                    if (navigationSelectItem != null)
                    {
                        if (match == null)
                            match = navigationItem;

                        var selectItemTranslator = new OeSelectItemTranslator(_edmModel, true);
                        selectItemTranslator.Translate(match, navigationSelectItem);
                    }
                }
                else
                {
                    if (hasSelectItems)
                        navigationItem.AddStructuralItem((IEdmStructuralProperty)propertyNode.Property, true);
                }

                orderByClause = orderByClause.ThenBy;
            }
        }
        public void BuildSelect(SelectExpandClause selectClause, OeMetadataLevel metadataLevel)
        {
            if (selectClause != null)
            {
                var selectItemTranslator = new OeSelectItemTranslator(_edmModel, false);
                foreach (SelectItem selectItemClause in selectClause.SelectedItems)
                    selectItemTranslator.Translate(_rootNavigationItem, selectItemClause);
            }

            _rootNavigationItem.AddKeyRecursive(metadataLevel != OeMetadataLevel.Full);
        }
        private Expression BuildSkipTakeSource(Expression source, OeSkipTokenNameValue[] skipTokenNameValues, bool isDatabaseNullHighestValue)
        {
            BuildOrderBySkipTake(_rootNavigationItem, _odataUri.OrderBy, HasSelectItems(_odataUri.SelectAndExpand));
            source = BuildJoin(source, new OeNavigationSelectItem[] { _rootNavigationItem });

            long? top = GetTop(_rootNavigationItem, _odataUri.Top);
            if (top == null && _odataUri.Skip == null)
                return source;

            var expressionBuilder = new OeExpressionBuilder(_joinBuilder);
            source = expressionBuilder.ApplySkipToken(source, skipTokenNameValues, _odataUri.OrderBy, isDatabaseNullHighestValue);
            source = expressionBuilder.ApplyOrderBy(source, _odataUri.OrderBy);
            source = expressionBuilder.ApplySkip(source, _odataUri.Skip, _odataUri.Path);
            return expressionBuilder.ApplyTake(source, top, _odataUri.Path);

            static bool HasSelectItems(SelectExpandClause selectExpandClause)
            {
                if (selectExpandClause != null)
                    foreach (SelectItem odataSelectItem in selectExpandClause.SelectedItems)
                        if (odataSelectItem is PathSelectItem pathSelectItem && pathSelectItem.SelectedPath.LastSegment is PropertySegment)
                            return true;

                return false;
            }
        }
        private MethodCallExpression CreateSelectExpression(Expression source)
        {
            if (_rootNavigationItem.HasNavigationItems())
                return (MethodCallExpression)source;

            if (_rootNavigationItem.AllSelected)
                return (MethodCallExpression)source;

            ParameterExpression parameter = _joinBuilder.Visitor.Parameter;
            IReadOnlyList<OeStructuralSelectItem> structuralItems = _rootNavigationItem.GetStructuralItemsWithNotSelected();
            var expressions = new Expression[structuralItems.Count];
            for (int i = 0; i < expressions.Length; i++)
                if (structuralItems[i].EdmProperty is ComputeProperty computeProperty)
                    expressions[i] = computeProperty.Expression;
                else
                {
                    PropertyInfo clrProperty = parameter.Type.GetPropertyIgnoreCase(structuralItems[i].EdmProperty);
                    expressions[i] = Expression.MakeMemberAccess(parameter, clrProperty);
                }
            NewExpression newTupleExpression = OeExpressionHelper.CreateTupleExpression(expressions);

            LambdaExpression lambda = Expression.Lambda(newTupleExpression, parameter);
            MethodInfo selectMethodInfo = OeMethodInfoHelper.GetSelectMethodInfo(parameter.Type, newTupleExpression.Type);
            return Expression.Call(selectMethodInfo, source, lambda);
        }
        internal static List<OeNavigationSelectItem> FlattenNavigationItems(OeNavigationSelectItem root, bool includeNextLink)
        {
            var navigationItems = new List<OeNavigationSelectItem>();
            var stack = new Stack<ValueTuple<OeNavigationSelectItem, int>>();
            stack.Push(new ValueTuple<OeNavigationSelectItem, int>(root, 0));
            do
            {
                ValueTuple<OeNavigationSelectItem, int> stackItem = stack.Pop();
                if (stackItem.Item2 == 0 && (includeNextLink || stackItem.Item1.Kind != OeNavigationSelectItemKind.NextLink))
                    navigationItems.Add(stackItem.Item1);

                if (stackItem.Item2 < stackItem.Item1.NavigationItems.Count)
                {
                    stack.Push(new ValueTuple<OeNavigationSelectItem, int>(stackItem.Item1, stackItem.Item2 + 1));
                    OeNavigationSelectItem selectItem = stackItem.Item1.NavigationItems[stackItem.Item2];
                    stack.Push(new ValueTuple<OeNavigationSelectItem, int>(selectItem, 0));
                }
            }
            while (stack.Count > 0);
            return navigationItems;
        }
        private Expression GetInnerSource(OeNavigationSelectItem navigationItem)
        {
            if (navigationItem.Parent == null)
                throw new InvalidOperationException("Inner source cannot exist for root item");

            Type clrEntityType = _edmModel.GetClrType(navigationItem.EdmProperty.DeclaringType);
            PropertyInfo navigationClrProperty = clrEntityType.GetPropertyIgnoreCase(navigationItem.EdmProperty);

            Type itemType = OeExpressionHelper.GetCollectionItemTypeOrNull(navigationClrProperty.PropertyType) ?? navigationClrProperty.PropertyType;
            var visitor = new OeQueryNodeVisitor(_joinBuilder.Visitor, Expression.Parameter(itemType));
            var expressionBuilder = new OeExpressionBuilder(_joinBuilder, visitor);

            IEdmNavigationProperty navigationProperty = navigationItem.EdmProperty;
            if (navigationItem.EdmProperty.ContainsTarget)
            {
                ModelBuilder.ManyToManyJoinDescription joinDescription = _edmModel.GetManyToManyJoinDescription(navigationProperty);
                navigationProperty = joinDescription.TargetNavigationProperty;
            }
            IEdmEntitySet innerEntitySet = OeEdmClrHelper.GetEntitySet(_edmModel, navigationProperty);
            Expression innerSource = OeEnumerableStub.CreateEnumerableStubExpression(itemType, innerEntitySet);

            ExpandedNavigationSelectItem item = navigationItem.NavigationSelectItem;
            innerSource = expressionBuilder.ApplyFilter(innerSource, item.FilterOption);

            long? top = GetTop(navigationItem, item.TopOption);
            if (top == null && item.SkipOption == null)
                return innerSource;

            OrderByClause orderByClause = item.OrderByOption;
            if (navigationItem.PageSize > 0)
                orderByClause = OeSkipTokenParser.GetUniqueOrderBy(navigationItem.EntitySet, item.OrderByOption, null);

            var entitySet = (IEdmEntitySet)navigationItem.Parent.EntitySet;
            Expression source = OeEnumerableStub.CreateEnumerableStubExpression(navigationClrProperty.DeclaringType!, entitySet);

            var crossApplyBuilder = new OeCrossApplyBuilder(_edmModel, expressionBuilder);
            return crossApplyBuilder.Build(source, innerSource, navigationItem.Path, orderByClause, item.SkipOption, top);
        }
        private static long? GetTop(OeNavigationSelectItem navigationItem, long? top)
        {
            if (navigationItem.PageSize > 0 && (top == null || navigationItem.PageSize < top.GetValueOrDefault()))
                top = navigationItem.PageSize;

            return top;
        }
        private static Expression SelectStructuralProperties(Expression source, OeNavigationSelectItem root)
        {
            if (!root.HasNavigationItems())
                return source;

            ParameterExpression parameter = Expression.Parameter(OeExpressionHelper.GetCollectionItemType(source.Type));
            IReadOnlyList<MemberExpression> joins = OeExpressionHelper.GetPropertyExpressions(parameter);
            var newJoins = new Expression[joins.Count];

            List<OeNavigationSelectItem> navigationItems = FlattenNavigationItems(root, false);
            bool isNavigationNullable = false;
            for (int i = 0; i < navigationItems.Count; i++)
            {
                newJoins[i] = joins[i];
                isNavigationNullable |= i > 0 && navigationItems[i].EdmProperty.Type.IsNullable;
                if (!navigationItems[i].AllSelected)
                {
                    IReadOnlyList<OeStructuralSelectItem> structuralItems = navigationItems[i].GetStructuralItemsWithNotSelected();
                    if (structuralItems.Count > 0)
                    {
                        var properties = new Expression[structuralItems.Count];
                        for (int j = 0; j < structuralItems.Count; j++)
                            if (structuralItems[j].EdmProperty is ComputeProperty computeProperty)
                                properties[j] = new ReplaceParameterVisitor(joins[i]).Visit(computeProperty.Expression);
                            else
                            {
                                PropertyInfo property = joins[i].Type.GetPropertyIgnoreCase(structuralItems[j].EdmProperty);
                                properties[j] = Expression.Property(joins[i], property);
                            }
                        Expression newTupleExpression = OeExpressionHelper.CreateTupleExpression(properties);

                        if (isNavigationNullable)
                        {
                            UnaryExpression nullConstant = Expression.Convert(OeConstantToVariableVisitor.NullConstantExpression, newTupleExpression.Type);
                            newTupleExpression = Expression.Condition(Expression.Equal(joins[i], OeConstantToVariableVisitor.NullConstantExpression), nullConstant, newTupleExpression);
                        }
                        newJoins[i] = newTupleExpression;
                    }
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
