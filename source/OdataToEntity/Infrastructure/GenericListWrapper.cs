using System;
using System.Collections;
using System.Collections.Generic;

namespace OdataToEntity.Infrastructure
{
    internal abstract class GenericListWrapper
    {
        private sealed class ListWrapper<T> : GenericListWrapper
        {
            private readonly List<T> _list;

            public ListWrapper()
            {
                _list = new List<T>();
            }

            public override bool Add(Object item)
            {
                var typedItem = (T)item;
                if (_list.Contains(typedItem))
                {
                    _list.Add(typedItem);
                    return true;
                }

                return false;
            }

            public override IList List => _list;
        }

        private GenericListWrapper()
        {
        }

        public static GenericListWrapper Create(Type itemType)
        {
            Type listWrapperType = typeof(ListWrapper<>).MakeGenericType(itemType);
            return (GenericListWrapper)Activator.CreateInstance(listWrapperType);
        }
        public abstract bool Add(Object item);

        public abstract IList List { get; }
    }
}
