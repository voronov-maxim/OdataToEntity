using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;

namespace OdataToEntity.Infrastructure
{
    public interface IGenericListWrapper
    {
        void Add(Object item);

        IList List { get; }
    }

    public static class GenericListWrapper
    {
        private static ConcurrentDictionary<Type, Func<int, IGenericListWrapper>> _createFuncs = new ConcurrentDictionary<Type, Func<int, IGenericListWrapper>>();

        public static IGenericListWrapper Create(Type itemType, int capacity = 4)
        {
            if (!_createFuncs.TryGetValue(itemType, out Func<int, IGenericListWrapper> createFunc))
            {
                Type listWrapperType = typeof(GenericListWrapper<>).MakeGenericType(itemType);
                MethodInfo ctorMethodInfo = listWrapperType.GetMethod(nameof(GenericListWrapper<Object>.Create));
                createFunc = (Func<int, IGenericListWrapper>)ctorMethodInfo.CreateDelegate(typeof(Func<int, IGenericListWrapper>));
                _createFuncs.TryAdd(itemType, createFunc);
            }

            return createFunc(capacity);
        }
    }

    public readonly struct GenericListWrapper<T> : IGenericListWrapper
    {
        private readonly List<T> _list;

        public GenericListWrapper(int capacity)
        {
            _list = new List<T>(capacity);
        }

        public void Add(Object item)
        {
            _list.Add((T)item);
        }
        public static IGenericListWrapper Create(int capacity)
        {
            return new GenericListWrapper<T>(capacity);
        }

        public IList List => _list;

    }
}
