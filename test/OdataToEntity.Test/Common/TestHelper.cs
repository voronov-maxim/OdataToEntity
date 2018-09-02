using Microsoft.EntityFrameworkCore;
using Microsoft.OData.Edm;
using Microsoft.OData.Edm.Csdl;
using Microsoft.OData.Edm.Validation;
using Newtonsoft.Json;
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
            private readonly Object _dataContext;
            private readonly Db.OeEntitySetAdapterCollection _entitySetAdapters;
            private IQueryable<T> _query;

            public QueryVisitor(Db.OeEntitySetAdapterCollection entitySetAdapters, Object dataContext)
            {
                _entitySetAdapters = entitySetAdapters;
                _dataContext = dataContext;
            }

            private ConstantExpression GetQueryConstantExpression(Expression node)
            {
                if (node.Type.IsGenericType && node.Type.GetGenericTypeDefinition() == typeof(IQueryable<>))
                {
                    Db.OeEntitySetAdapter entitySetAdapter = _entitySetAdapters.FindByClrType(node.Type.GetGenericArguments()[0]);
                    IQueryable query = entitySetAdapter.GetEntitySet(_dataContext);
                    if (_query == null && entitySetAdapter.EntityType == typeof(T))
                        _query = (IQueryable<T>)query;

                    return Expression.Constant(query);
                }

                return null;
            }
            protected override Expression VisitParameter(ParameterExpression node)
            {
                return GetQueryConstantExpression(node) ?? base.VisitParameter(node);
            }
            protected override Expression VisitMember(MemberExpression node)
            {
                return GetQueryConstantExpression(node) ?? base.VisitMember(node);
            }

            public IQueryable<T> Query => _query;
        }

        public static void Compare(IList fromDb, IList fromOe, ModelBuilder.OeEdmModelMetadataProvider metadataProvider, IReadOnlyList<IncludeVisitor.Include> includes)
        {
            var contractResolver = new TestContractResolver(metadataProvider, includes);
            var settings = new JsonSerializerSettings()
            {
                ContractResolver = contractResolver,
                DateFormatString = "yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'ffffff",
                DateTimeZoneHandling = DateTimeZoneHandling.Utc,
                Formatting = Newtonsoft.Json.Formatting.Indented,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                NullValueHandling = NullValueHandling.Ignore
            };

            String jsonDb = RemoveEmptyArrays(JsonConvert.SerializeObject(fromDb, settings));
            contractResolver.DisableWhereOrder = true;
            settings.ReferenceLoopHandling = ReferenceLoopHandling.Error;
            String jsonOe = RemoveEmptyArrays(JsonConvert.SerializeObject(fromOe, settings));

            Assert.Equal(jsonDb, jsonOe);
        }
        public static Infrastructure.OeEntryEqualityComparer CreateEntryEqualityComparer(ModelBuilder.OeEdmModelMetadataProvider metadataProvider, Type entityType)
        {
            return new Infrastructure.OeEntryEqualityComparer(GetKeyExpressions(metadataProvider, entityType));
        }
        public static IList ExecuteDb<T, TResult>(Db.OeEntitySetAdapterCollection entitySetAdapters, DbContext dataContext, Expression<Func<IQueryable<T>, TResult>> expression)
        {
            var visitor = new QueryVisitor<T>(entitySetAdapters, dataContext);
            Expression call = visitor.Visit(expression.Body);
            return new[] { visitor.Query.Provider.Execute<TResult>(call) };
        }
        public static IList ExecuteDb<T, TResult>(Db.OeDataAdapter dataAdapter, DbContext dataContext,
            Expression<Func<IQueryable<T>, IQueryable<TResult>>> expression, out IReadOnlyList<IncludeVisitor.Include> includes)
        {
            var visitor = new QueryVisitor<T>(dataAdapter.EntitySetAdapters, dataContext);
            Expression call = visitor.Visit(expression.Body);

            var metadataProvider = new OdataToEntity.EfCore.OeEfCoreEdmModelMetadataProvider(dataContext.Model);
            var includeVisitor = new IncludeVisitor(metadataProvider, dataAdapter.IsDatabaseNullHighestValue);
            call = includeVisitor.Visit(call);
            includes = includeVisitor.Includes;

            IList fromDb = visitor.Query.Provider.CreateQuery<TResult>(call).ToList();
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
                    if (!CsdlWriter.TryWriteCsdl(edmModel, xmlWriter, CsdlTarget.OData, out IEnumerable<EdmError> errors))
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
        private static MemberExpression[] GetKeyExpressions(ModelBuilder.OeEdmModelMetadataProvider metadataProvider, Type entityType)
        {
            var keyPropertyList = new List<PropertyInfo>();
            foreach (PropertyInfo property in entityType.GetProperties())
                if (metadataProvider.IsKey(property))
                    keyPropertyList.Add(property);

            if (keyPropertyList.Count == 0)
            {
                PropertyInfo keyProperty = entityType.GetPropertyIgnoreCase("Id");
                if (keyProperty == null)
                    throw new InvalidOperationException("Key not found in " + entityType.Name);

                keyPropertyList.Add(keyProperty);
            }
            PropertyInfo[] keyProperties = keyPropertyList.ToArray();
            metadataProvider.SortClrPropertyByOrder(keyProperties);

            var keyExpressions = new MemberExpression[keyProperties.Length];
            ParameterExpression parameter = Expression.Parameter(entityType);
            for (int i = 0; i < keyProperties.Length; i++)
                keyExpressions[i] = Expression.Property(parameter, keyProperties[i]);
            return keyExpressions;
        }
        private static String RemoveEmptyArrays(String json)
        {
            String[] lines = json.Split(Environment.NewLine);
            for (int i = 1; i < lines.Length; i++)
                if (lines[i].EndsWith(": []"))
                {
                    if (lines[i - 1].EndsWith(','))
                        lines[i - 1] = lines[i - 1].Remove(lines[i - 1].Length - 1, 1);
                    lines[i] = null;
                }
                else if (lines[i].EndsWith(": [],"))
                    lines[i] = null;

            var stringBuilder = new StringBuilder(json.Length);
            for (int i = 0; i < lines.Length; i++)
                if (lines[i] != null)
                    stringBuilder.AppendLine(lines[i]);
            return stringBuilder.ToString();
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
