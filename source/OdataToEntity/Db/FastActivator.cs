using System;
using System.Linq.Expressions;
using System.Reflection;

namespace OdataToEntity.Db
{
    public static class FastActivator
    {
        public static T CreateInstance<T>() => FastActivatorImpl<T>.Factory();

        private static class FastActivatorImpl<T>
        {
            public static readonly Func<T> Factory = GetFactory();

            private static Func<T> GetFactory()
            {
                ConstructorInfo ctor = typeof(T).GetTypeInfo().GetConstructor(Type.EmptyTypes);
                return (Func<T>)Expression.Lambda(Expression.New(ctor)).Compile();
            }
        }
    }
}
