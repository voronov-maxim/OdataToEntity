using System;
using System.Linq;
using System.Linq.Expressions;

namespace OdataToEntity.Test
{
    public class QueryParameters<T, TResult>
    {
        public String RequestUri { get; set; }
        public Expression<Func<IQueryable<T>, IQueryable<TResult>>> Expression { get; set; }
        public bool NavigationNextLink { get; set; }
        public int PageSize { get; set; }
    }

    public sealed class QueryParameters<T> : QueryParameters<T, T>
    {
    }

    public sealed class QueryParametersScalar<T, TResult>
    {
        public String RequestUri { get; set; }
        public Expression<Func<IQueryable<T>, TResult>> Expression { get; set; }
    }
}
