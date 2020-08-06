using Microsoft.OData.Edm;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace OdataToEntity.Parsers
{
    public class OeEnumerableStub
    {
        private sealed class OeEnumerableStubImpl<T> : OeEnumerableStub, IEnumerable<T>, IQueryable<T>
        {
            private OeEnumerableStubImpl(IEdmEntitySet entitySet) : base(typeof(T), entitySet)
            {
            }

            public static OeEnumerableStub Create(IEdmEntitySet entitySet)
            {
                return new OeEnumerableStubImpl<T>(entitySet);
            }
            public IEnumerator<T> GetEnumerator()
            {
                throw new NotSupportedException("IEnumerable stub source");
            }
            IEnumerator IEnumerable.GetEnumerator()
            {
                throw new NotSupportedException("IEnumerable Stub source");
            }

            public Expression Expression => throw new NotSupportedException("IEnumerable stub source");
            public IQueryProvider Provider => throw new NotSupportedException("IEnumerable stub source");
        }

        protected OeEnumerableStub(Type entityType, IEdmEntitySet entitySet)
        {
            ElementType = entityType;
            EntitySet = entitySet;
        }

        private static OeEnumerableStub Create(Type entityType, IEdmEntitySet entitySet)
        {
            Type stubType = typeof(OeEnumerableStubImpl<>).MakeGenericType(entityType);
            MethodInfo createMethodInfo = stubType.GetMethod(nameof(OeEnumerableStubImpl<Object>.Create))!;
            return (OeEnumerableStub)createMethodInfo.Invoke(null, new Object[] { entitySet })!;
        }
        public static ConstantExpression CreateEnumerableStubExpression(Type entityType, IEdmEntitySet entitySet)
        {
            return Expression.Constant(Create(entityType, entitySet), typeof(IEnumerable<>).MakeGenericType(entityType));
        }

        public Type ElementType { get; }
        public IEdmEntitySet EntitySet { get; }
    }
}
