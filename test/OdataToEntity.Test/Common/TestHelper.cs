using Microsoft.EntityFrameworkCore;
using Microsoft.OData.Edm;
using Microsoft.OData.Edm.Csdl;
using Microsoft.OData.Edm.Validation;
using Newtonsoft.Json;
using OdataToEntity.Test.Model;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Xml;
using Xunit;

namespace OdataToEntity.Test
{
    internal partial class TestHelper
    {
        private sealed class QueryVisitor<T> : ExpressionVisitor
        {
            private readonly IQueryable _query;
            private ConstantExpression _parameter;

            public QueryVisitor(IQueryable query)
            {
                _query = query;
            }

            protected override Expression VisitParameter(ParameterExpression node)
            {
                if (_parameter == null && node.Type == typeof(IQueryable<T>))
                {
                    _parameter = Expression.Constant(_query);
                    return _parameter;
                }
                return base.VisitParameter(node);
            }
        }

        public static void Compare(IList fromDb, IList fromOe, IReadOnlyList<IncludeVisitor.Include> includes)
        {
            var settings = new JsonSerializerSettings()
            {
                ContractResolver = new TestContractResolver(includes),
                DateFormatString = "yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'ffffff",
                DateTimeZoneHandling = DateTimeZoneHandling.Utc,
                Formatting = Newtonsoft.Json.Formatting.Indented,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                NullValueHandling = NullValueHandling.Ignore
            };
            String jsonDb = JsonConvert.SerializeObject(fromDb, settings);
            String jsonOe = JsonConvert.SerializeObject(fromOe, settings);

            Assert.Equal(jsonDb, jsonOe);
        }
        public static IList ExecuteDb<T, TResult>(DbContext dataContext, Expression<Func<IQueryable<T>, TResult>> expression)
        {
            IQueryable<T> query = GetQuerableDb<T>(dataContext);
            var visitor = new QueryVisitor<T>(query);
            Expression call = visitor.Visit(expression.Body);
            return new[] { query.Provider.Execute<TResult>(call) };
        }
        public static IList ExecuteDb<T, TResult>(DbContext dataContext, Expression<Func<IQueryable<T>, IQueryable<TResult>>> expression, out IReadOnlyList<IncludeVisitor.Include> includes)
        {
            IQueryable<T> query = GetQuerableDb<T>(dataContext);
            var visitor = new QueryVisitor<T>(query);
            Expression call = visitor.Visit(expression.Body);

            var includeVisitor = new IncludeVisitor();
            call = includeVisitor.Visit(call);
            includes = includeVisitor.Includes;

            IList fromDb = query.Provider.CreateQuery<TResult>(call).ToList();
            if (typeof(TResult) == typeof(Object))
                fromDb = ToOpenType(fromDb);

            return fromDb;
        }
        public static String GetCsdlSchema(IEdmModel edmModel)
        {
            using (var stream = new MemoryStream())
            {
                using (XmlWriter xmlWriter = XmlWriter.Create(stream, new XmlWriterSettings() { Indent = true }))
                {
                    IEnumerable<EdmError> errors;
#if ODATA6
                    if (!CsdlWriter.TryWriteCsdl(edmModel, xmlWriter, out errors))
#else
                    if (!CsdlWriter.TryWriteCsdl(edmModel, xmlWriter, CsdlTarget.OData, out errors))
#endif
                        return null;
                }

                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }
        private static Type GetCollectionItemType(Type collectionType)
        {
            if (collectionType.IsPrimitive)
                return null;

            if (collectionType.IsGenericType && collectionType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                return collectionType.GetGenericArguments()[0];

            foreach (Type iface in collectionType.GetInterfaces())
                if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                    return iface.GetGenericArguments()[0];
            return null;
        }
        private static IQueryable<T> GetQuerableDb<T>(DbContext dataContext)
        {
            var orderContext = (OrderContext)dataContext;
            if (typeof(T) == typeof(Customer))
                return (IQueryable<T>)orderContext.Customers;
            if (typeof(T) == typeof(OrderItem))
                return (IQueryable<T>)orderContext.OrderItems;
            if (typeof(T) == typeof(Order))
                return (IQueryable<T>)orderContext.Orders;
            if (typeof(T) == typeof(Category))
                return (IQueryable<T>)orderContext.Categories;
            if (typeof(T) == typeof(ManyColumns))
                return (IQueryable<T>)orderContext.ManyColumns;

            throw new InvalidOperationException("unknown type " + typeof(T).Name);
        }
        public static IList ToOpenType(IEnumerable entities)
        {
            var openTypes = new List<SortedDictionary<String, Object>>();
            foreach (Object entity in entities)
            {
                var openType = new SortedDictionary<String, Object>(StringComparer.Ordinal);
                foreach (PropertyInfo property in entity.GetType().GetProperties())
                    openType.Add(property.Name, property.GetValue(entity));
                openTypes.Add(openType);
            }
            return openTypes;
        }
    }
}
