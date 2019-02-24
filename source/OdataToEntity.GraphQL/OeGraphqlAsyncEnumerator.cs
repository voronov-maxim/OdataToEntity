using Microsoft.OData.UriParser;
using OdataToEntity.Parsers;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity.GraphQL
{
    public sealed class OeGraphqlAsyncEnumerator : IAsyncEnumerator<Dictionary<String, Object>>
    {
        private sealed class SetPropertyValueVisitor : ExpressionVisitor
        {
            private Dictionary<String, Object> _entity;
            private Object _orderValue;
            private MemberExpression _propertyExpression;

            public void SetPropertyValue(Dictionary<String, Object> entity, MemberExpression propertyExpression, Object orderValue)
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
                Object propertyValue = _entity[property.Name];
                if (propertyValue == null)
                {
                    if (node == _propertyExpression)
                        propertyValue = _orderValue;
                    else
                        propertyValue = Activator.CreateInstance(property.PropertyType);
                    property.SetValue(_entity, propertyValue);
                }
                _entity = new Dictionary<String, Object>() { { property.Name, propertyValue } };

                return node;
            }
        }

        private readonly Db.OeDbEnumerator _dbEnumerator;
        private readonly IDisposable _dispose;
        private bool _isFirstMoveNext;
        private bool _isMoveNext;
        private readonly OeQueryContext _queryContext;

        public OeGraphqlAsyncEnumerator(Db.OeAsyncEnumerator asyncEnumerator, OeEntryFactory entryFactory, OeQueryContext queryContext)
        {
            _dispose = asyncEnumerator;
            _dbEnumerator = new Db.OeDbEnumerator(asyncEnumerator, entryFactory);
            _queryContext = queryContext;
            _isFirstMoveNext = true;
        }

        private static async Task<Dictionary<String, Object>> CreateEntity(Db.OeDbEnumerator dbEnumerator, Object value, Object entity)
        {
            if (OeExpressionHelper.IsTupleType(entity.GetType()))
                value = entity;

            Dictionary<String, Object> dictionary = CreateEntity(entity, dbEnumerator.EntryFactory.Accessors);
            foreach (OeEntryFactory navigationLink in dbEnumerator.EntryFactory.NavigationLinks)
            {
                var childDbEnumerator = (Db.OeDbEnumerator)dbEnumerator.CreateChild(navigationLink);
                await SetNavigationProperty(childDbEnumerator, value, dictionary).ConfigureAwait(false);
            }

            return dictionary;
        }
        private static Dictionary<String, Object> CreateEntity(Object value, OePropertyAccessor[] accessors)
        {
            var entity = new Dictionary<String, Object>(accessors.Length);
            for (int i = 0; i < accessors.Length; i++)
            {
                OePropertyAccessor accessor = accessors[i];
                entity[accessor.EdmProperty.Name] = accessor.GetValue(value);
            }
            return entity;
        }
        private static async Task<Object> CreateNestedEntity(Db.OeDbEnumerator dbEnumerator, Object value)
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
                        entityList.Add(await CreateEntity(dbEnumerator, item, item).ConfigureAwait(false));
                }
                while (await dbEnumerator.MoveNextAsync().ConfigureAwait(false));
                return entityList;
            }

            return await CreateEntity(dbEnumerator, value, entity).ConfigureAwait(false);
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

            Dictionary<String, Object> entity = await CreateEntity(_dbEnumerator, _dbEnumerator.Current, _dbEnumerator.Current).ConfigureAwait(false);
            Object buffer = _dbEnumerator.ClearBuffer();

            _isMoveNext = await _dbEnumerator.MoveNextAsync().ConfigureAwait(false);
            if (!_isMoveNext && _queryContext.EntryFactory.SkipTokenAccessors.Length > 0)
                SetOrderByProperties(_queryContext, entity, buffer);

            Current = entity;
            return true;
        }
        private static async Task SetNavigationProperty(Db.OeDbEnumerator dbEnumerator, Object value, Dictionary<String, Object> entity)
        {
            Object navigationValue = await CreateNestedEntity(dbEnumerator, value).ConfigureAwait(false);
            entity[dbEnumerator.EntryFactory.ResourceInfo.Name] = navigationValue;
        }
        private static void SetOrderByProperties(OeQueryContext queryContext, Dictionary<String, Object> entity, Object value)
        {
            var visitor = new OeQueryNodeVisitor(queryContext.EdmModel, Expression.Parameter(queryContext.EntryFactory.ClrEntityType));
            var setPropertyValueVisitor = new SetPropertyValueVisitor();

            int i = 0;
            OrderByClause orderByClause = queryContext.ODataUri.OrderBy;
            while (orderByClause != null)
            {
                var propertyExpression = (MemberExpression)visitor.TranslateNode(orderByClause.Expression);
                Object orderValue = queryContext.EntryFactory.SkipTokenAccessors[i++].GetValue(value);
                setPropertyValueVisitor.SetPropertyValue(entity, propertyExpression, orderValue);

                orderByClause = orderByClause.ThenBy;
            }
        }

        public Dictionary<String, Object> Current { get; private set; }
    }
}
