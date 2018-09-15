using Microsoft.OData.UriParser;
using OdataToEntity.Parsers;
using System;
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

        private readonly Db.OeDbEnumerator _dbEnumerator;
        private readonly IDisposable _dispose;
        private bool _isFirstMoveNext;
        private bool _isMoveNext;
        private readonly OeQueryContext _queryContext;

        public OeEntityAsyncEnumerator(Db.OeAsyncEnumerator asyncEnumerator, OeEntryFactory entryFactory, OeQueryContext queryContext)
        {
            _dispose = asyncEnumerator;
            _dbEnumerator = new Db.OeDbEnumerator(asyncEnumerator, entryFactory);
            _queryContext = queryContext;
            _isFirstMoveNext = true;
        }

        private static async Task<Object> CreateEntity(Db.OeDbEnumerator dbEnumerator, Object value, Object entity, Type entityType)
        {
            if (OeExpressionHelper.IsTupleType(entity.GetType()))
            {
                value = entity;
                entity = CreateEntityFromTuple(entityType, entity, dbEnumerator.EntryFactory.Accessors);
            }

            foreach (OeEntryFactory navigationLink in dbEnumerator.EntryFactory.NavigationLinks)
                await SetNavigationProperty(dbEnumerator.CreateChild(navigationLink), value, entity).ConfigureAwait(false);

            return entity;
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
        private static async Task<Object> CreateNestedEntity(Db.OeDbEnumerator dbEnumerator, Object value, Type nestedEntityType)
        {
            Object entity = dbEnumerator.Current;
            if (entity == null)
                return null;

            if (dbEnumerator.EntryFactory.ResourceInfo.IsCollection.GetValueOrDefault())
            {
                var entityList = new List<Object>();
                do
                {
                    Object item = dbEnumerator.Current;
                    if (item != null)
                        entityList.Add(await CreateEntity(dbEnumerator, item, item, nestedEntityType).ConfigureAwait(false));
                }
                while (await dbEnumerator.MoveNextAsync().ConfigureAwait(false));

                var entities = (Object[])Array.CreateInstance(nestedEntityType, entityList.Count);
                entityList.CopyTo(entities);
                return entities;
            }

            return await CreateEntity(dbEnumerator, value, entity, nestedEntityType).ConfigureAwait(false);
        }
        public void Dispose()
        {
            _dispose.Dispose();
        }
        public async Task<bool> MoveNext(CancellationToken cancellationToken)
        {
            if (_isFirstMoveNext)
            {
                _isFirstMoveNext = false;
                _isMoveNext = await _dbEnumerator.MoveNextAsync().ConfigureAwait(false);
            }
            if (!_isMoveNext)
                return false;

            var entity = (T)await CreateEntity(_dbEnumerator, _dbEnumerator.Current, _dbEnumerator.Current, typeof(T)).ConfigureAwait(false);
            Object buffer = _dbEnumerator.ClearBuffer();

            _isMoveNext = await _dbEnumerator.MoveNextAsync().ConfigureAwait(false);
            if (!_isMoveNext && _queryContext.SkipTokenNameValues != null && _queryContext.SkipTokenAccessors != null)
                SetOrderByProperties(_queryContext, entity, buffer);

            Current = entity;
            return true;
        }
        private static async Task SetNavigationProperty(Db.OeDbEnumerator dbEnumerator, Object value, Object entity)
        {
            PropertyInfo propertyInfo = entity.GetType().GetProperty(dbEnumerator.EntryFactory.ResourceInfo.Name);
            Type nestedEntityType = OeExpressionHelper.GetCollectionItemType(propertyInfo.PropertyType);
            if (nestedEntityType == null)
                nestedEntityType = propertyInfo.PropertyType;

            Object navigationValue = await CreateNestedEntity(dbEnumerator, value, nestedEntityType).ConfigureAwait(false);
            propertyInfo.SetValue(entity, navigationValue);
        }
        private static void SetOrderByProperties(OeQueryContext queryContext, Object entity, Object value)
        {
            var visitor = new OeQueryNodeVisitor(queryContext.EdmModel, Expression.Parameter(typeof(T)));
            var setPropertyValueVisitor = new SetPropertyValueVisitor();

            int i = 0;
            OrderByClause orderByClause = queryContext.ODataUri.OrderBy;
            while (orderByClause != null)
            {
                var propertyExpression = (MemberExpression)visitor.TranslateNode(orderByClause.Expression);
                Object orderValue = queryContext.SkipTokenAccessors[i++].GetValue(value);
                setPropertyValueVisitor.SetPropertyValue(entity, propertyExpression, orderValue);

                orderByClause = orderByClause.ThenBy;
            }
        }

        public T Current { get; private set; }
    }
}
