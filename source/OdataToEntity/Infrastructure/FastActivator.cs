using System;
using System.Linq.Expressions;
using System.Reflection;

namespace OdataToEntity.Infrastructure
{
    public static class FastActivator
    {
        public static T CreateInstance<T>() => FastActivatorImpl<T>.Factory();

        private static class FastActivatorImpl<T>
        {
            public static readonly Func<T> Factory = GetFactory();

            private static Func<T> GetFactory()
            {
                ConstructorInfo? ctor = typeof(T).GetConstructor(Type.EmptyTypes);
                if (ctor == null)
                    throw new InvalidOperationException(typeof(T).Name + " must have default constructor or override OeDataAdapter.CreateDataContext()");

                return (Func<T>)Expression.Lambda(Expression.New(ctor)).Compile();
            }
        }
    }
}
