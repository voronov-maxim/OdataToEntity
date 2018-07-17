using Microsoft.OData.UriParser;
using OdataToEntity.Parsers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity.AspNetCore
{
    public sealed class OeEntityAsyncEnumerator<T> : IAsyncEnumerator<T>
    {
        private sealed class SetPropertyValueVisitor : ExpressionVisitor
        {
            private Object _entity;
            private Object _orderValue;
            private MemberExpression _propertyExpression;

            public void SetPropertyValue(Object entity, MemberExpression propertyExpression, Object orderValue)
            {
                _entity = entity;
                _propertyExpression = propertyExpression;
                _orderValue = orderValue;

                base.Visit(propertyExpression);
            }
            protected override Expression VisitMember(MemberExpression node)
            {
                base.VisitMember(node);

                var property = (PropertyInfo)node.Member;
                Object propertyValue = property.GetValue(_entity);
                if (propertyValue == null)
                {
                    if (node == _propertyExpression)
                        propertyValue = _orderValue;
                    else
                        propertyValue = Activator.CreateInstance(property.PropertyType);
                    property.SetValue(_entity, propertyValue);
                }
                _entity = propertyValue;

                return node;
            }
        }

        private readonly Db.OeAsyncEnumerator _asyncEnumerator;
        private readonly OeEntryFactory _entryFactory;
        private bool _isFirstMoveNext;
        private bool _isMoveNext;
        private readonly OeQueryContext _queryContext;

        public OeEntityAsyncEnumerator(OeEntryFactory entryFactory, Db.OeAsyncEnumerator asyncEnumerator)
        {
            _entryFactory = entryFactory;
            _asyncEnumerator = asyncEnumerator;

            _isFirstMoveNext = true;
        }
        public OeEntityAsyncEnumerator(OeEntryFactory entryFactory, Db.OeAsyncEnumerator asyncEnumerator, OeQueryContext queryContext)
            : this(entryFactory, asyncEnumerator)
        {
            _queryContext = queryContext;
        }

        private static Object CreateEntityFromTuple(Type entityType, Object tuple, OePropertyAccessor[] accessors)
        {
            Object entity = Activator.CreateInstance(entityType);
            for (int i = 0; i < accessors.Length; i++)
            {
                OePropertyAccessor accessor = accessors[i];
                Object value = accessor.GetValue(tuple);
                entityType.GetProperty(accessor.EdmProperty.Name).SetValue(entity, value);
            }
            return entity;
        }
        private static Object CreateNestedEntity(OeEntryFactory entryFactory, Object value, Type nestedEntityType)
        {
            Object navigationValue = entryFactory.GetValue(value, out int? dummy);
            if (navigationValue == null)
                return null;

            if (entryFactory.ResourceInfo.IsCollection.GetValueOrDefault())
            {
                var entityList = new List<Object>();
                foreach (Object item in (IEnumerable)navigationValue)
                    entityList.Add(CreateEntity(item));

                var entities = (Object[])Array.CreateInstance(nestedEntityType, entityList.Count);
                entityList.CopyTo(entities);
                return entities;
            }
            else
            {
                if (Writers.OeGetWriter.IsNullNavigationValue(navigationValue))
                    return null;
            }

            return CreateEntity(navigationValue);

            Object CreateEntity(Object entity)
            {
                if (OeExpressionHelper.IsTupleType(entity.GetType()))
                    entity = CreateEntityFromTuple(nestedEntityType, entity, entryFactory.Accessors);
                foreach (OeEntryFactory navigationLink in entryFactory.NavigationLinks)
                    SetNavigationProperty(navigationLink, value, entity);

                return entity;
            }
        }
        public void Dispose() => _asyncEnumerator.Dispose();
        public async Task<bool> MoveNext(CancellationToken cancellationToken)
        {
            if (_isFirstMoveNext)
            {
                _isFirstMoveNext = false;
                _isMoveNext = await _asyncEnumerator.MoveNextAsync().ConfigureAwait(false);
            }
            if (!_isMoveNext)
                return false;

            Object value = _asyncEnumerator.Current;
            Object entry = _entryFactory.GetValue(value, out int? dummy);
            if (OeExpressionHelper.IsTupleType(entry.GetType()))
                Current = (T)CreateEntityFromTuple(typeof(T), entry, _entryFactory.Accessors);
            else
                Current = (T)entry;

            foreach (OeEntryFactory navigationLink in _entryFactory.NavigationLinks)
                SetNavigationProperty(navigationLink, value, Current);

            _isMoveNext = await _asyncEnumerator.MoveNextAsync().ConfigureAwait(false);
            if (!_isMoveNext && _queryContext.SkipTokenNameValues != null && _queryContext.SkipTokenAccessors != null)
                SetOrderByProperties(Current, value);

            return true;
        }
        private static void SetNavigationProperty(OeEntryFactory navigationLink, Object value, Object ownerEntry)
        {
            PropertyInfo propertyInfo = ownerEntry.GetType().GetProperty(navigationLink.ResourceInfo.Name);
            Type nestedEntityType = OeExpressionHelper.GetCollectionItemType(propertyInfo.PropertyType);
            if (nestedEntityType == null)
                nestedEntityType = propertyInfo.PropertyType;

            Object navigationValue = CreateNestedEntity(navigationLink, value, nestedEntityType);
            propertyInfo.SetValue(ownerEntry, navigationValue);
        }
        private void SetOrderByProperties(Object entity, Object value)
        {
            var visitor = new OeQueryNodeVisitor(_queryContext.EdmModel, Expression.Parameter(typeof(T)));
            var setPropertyValueVisitor = new SetPropertyValueVisitor();

            int i = 0;
            OrderByClause orderByClause = _queryContext.ODataUri.OrderBy;
            while (orderByClause != null)
            {
                var propertyExpression = (MemberExpression)visitor.TranslateNode(orderByClause.Expression);
                Object orderValue = _queryContext.SkipTokenAccessors[i++].GetValue(value);
                setPropertyValueVisitor.SetPropertyValue(entity, propertyExpression, orderValue);

                orderByClause = orderByClause.ThenBy;
            }
        }

        public T Current { get; private set; }
    }
}
