using OdataToEntity.Parsers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity.AspNetCore
{
    public sealed class OeEntityAsyncEnumerator<T> : IAsyncEnumerator<T>
    {
        private readonly Db.OeAsyncEnumerator _asyncEnumerator;
        private readonly OeEntryFactory _entryFactory;

        public OeEntityAsyncEnumerator(OeEntryFactory entryFactory, Db.OeAsyncEnumerator asyncEnumerator)
        {
            _entryFactory = entryFactory;
            _asyncEnumerator = asyncEnumerator;
        }

        public T CreateEntityFromTuple(Object tuple, OePropertyAccessor[] accessors)
        {
            Type clrType = typeof(T);
            var entity = (T)Activator.CreateInstance(clrType);
            for (int i = 0; i < accessors.Length; i++)
            {
                OePropertyAccessor accessor = accessors[i];
                Object value = accessor.Accessor(tuple);
                clrType.GetProperty(accessor.Name).SetValue(entity, value);
            }
            return entity;
        }
        private static Object CreateNestedEntity(OeEntryFactory entryFactory, Object value)
        {
            Object navigationValue = entryFactory.GetValue(value, out int? dummy);
            if (navigationValue == null)
                return null;

            if (entryFactory.ResourceInfo.IsCollection.GetValueOrDefault())
            {
                foreach (Object entity in (IEnumerable)navigationValue)
                    foreach (OeEntryFactory navigationLink in entryFactory.NavigationLinks)
                        SetNavigationProperty(navigationLink, value, entity);
            }
            else
            {
                foreach (OeEntryFactory navigationLink in entryFactory.NavigationLinks)
                    SetNavigationProperty(navigationLink, value, navigationValue);
            }

            return navigationValue;
        }
        public void Dispose() => _asyncEnumerator.Dispose();
        public async Task<bool> MoveNext(CancellationToken cancellationToken)
        {
            if (await _asyncEnumerator.MoveNextAsync().ConfigureAwait(false))
            {
                Object value = _asyncEnumerator.Current;
                Object entry = _entryFactory.GetValue(value, out int? dummy);
                if (OeExpressionHelper.IsTupleType(entry.GetType()))
                    Current = CreateEntityFromTuple(entry, _entryFactory.Accessors);
                else
                    Current = (T)entry;

                foreach (OeEntryFactory navigationLink in _entryFactory.NavigationLinks)
                    SetNavigationProperty(navigationLink, value, Current);
                return true;
            }

            return false;
        }
        private static void SetNavigationProperty(OeEntryFactory navigationLink, Object value, Object ownerEntry)
        {
            Object navigationValue = CreateNestedEntity(navigationLink, value);
            PropertyInfo propertyInfo = ownerEntry.GetType().GetProperty(navigationLink.ResourceInfo.Name);
            propertyInfo.SetValue(ownerEntry, navigationValue);
        }

        public T Current { get; private set; }
    }
}
