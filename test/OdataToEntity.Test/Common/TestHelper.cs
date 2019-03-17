using Microsoft.EntityFrameworkCore;
using Microsoft.OData.Edm;
using Microsoft.OData.Edm.Csdl;
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
                    Db.OeEntitySetAdapter entitySetAdapter = FindEntitySetAdapterByClrType(_entitySetAdapters, node.Type.GetGenericArguments()[0]);
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

        public static void Compare(IList fromDb, IList fromOe, IReadOnlyList<EfInclude> includes)
        {
            fromDb = ToOpenType(fromDb, includes);
            fromOe = ToOpenType(fromOe, null);

            var settings = new JsonSerializerSettings()
            {
                DateFormatString = "yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'ffffff",
                DateTimeZoneHandling = DateTimeZoneHandling.Utc,
                Formatting = Newtonsoft.Json.Formatting.Indented,
                NullValueHandling = NullValueHandling.Ignore
            };

            String jsonDb = JsonConvert.SerializeObject(fromDb, settings);
            String jsonOe = JsonConvert.SerializeObject(fromOe, settings);

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
            Expression<Func<IQueryable<T>, IQueryable<TResult>>> expression, out IReadOnlyList<EfInclude> includes)
        {
            var visitor = new QueryVisitor<T>(dataAdapter.EntitySetAdapters, dataContext);
            Expression call = visitor.Visit(expression.Body);

            var metadataProvider = new OdataToEntity.EfCore.OeEfCoreEdmModelMetadataProvider(dataContext.Model);
            var includeVisitor = new IncludeVisitor(metadataProvider, dataAdapter.IsDatabaseNullHighestValue);
            call = includeVisitor.Visit(call);
            includes = includeVisitor.Includes;

            return visitor.Query.Provider.CreateQuery<TResult>(call).ToList();
        }
        public static Db.OeEntitySetAdapter FindEntitySetAdapterByClrType(Db.OeEntitySetAdapterCollection entitySetAdapters, Type entityType)
        {
            foreach (Db.OeEntitySetAdapter entitySetAdapter in entitySetAdapters)
                if (entitySetAdapter.EntityType.FullName == entityType.FullName) //linq2db test use different data context
                    return entitySetAdapter;

            throw new InvalidOperationException("EntitySetAdapter not found for type " + entityType.FullName);
        }
        public static Db.OeEntitySetAdapter FindEntitySetAdapterByName(Db.OeEntitySetAdapterCollection entitySetAdapters, String entitySetName)
        {
            foreach (Db.OeEntitySetAdapter entitySetAdapter in entitySetAdapters)
                if (String.Compare(entitySetAdapter.EntitySetName, entitySetName, StringComparison.OrdinalIgnoreCase) == 0)
                    return entitySetAdapter;

            throw new InvalidOperationException("EntitySetAdapter not found for name " + entitySetName);
        }
        public static Db.OeEntitySetAdapter FindEntitySetAdapterByTypeName(Db.OeEntitySetAdapterCollection entitySetAdapters, String typeName)
        {
            foreach (Db.OeEntitySetAdapter entitySetAdapter in entitySetAdapters)
                if (String.Compare(entitySetAdapter.EntityType.FullName, typeName, StringComparison.OrdinalIgnoreCase) == 0)
                    return entitySetAdapter;

            throw new InvalidOperationException("EntitySetAdapter not found for type name " + typeName);
        }
        public static String GetCsdlSchema(IEdmModel edmModel)
        {
            using (var stream = new MemoryStream())
            {
                using (XmlWriter xmlWriter = XmlWriter.Create(stream, new XmlWriterSettings() { Indent = true, Encoding = new UTF8Encoding(false) }))
                {
                    if (!CsdlWriter.TryWriteCsdl(edmModel, xmlWriter, CsdlTarget.OData, out _))
                        return null;
                }

                return Encoding.UTF8.GetString(stream.ToArray());
            }
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
        public static Cache.OeQueryCache GetQueryCache(Db.OeDataAdapter dataAdapter)
        {
            PropertyInfo propertyInfo = typeof(Db.OeDataAdapter).GetProperty("QueryCache", BindingFlags.Instance | BindingFlags.NonPublic);
            return (Cache.OeQueryCache)propertyInfo.GetValue(dataAdapter);
        }
        public static int GetQueryCacheCount(IEdmModel edmModel)
        {
            Db.OeDataAdapter dataAdapter = edmModel.GetDataAdapter(edmModel.EntityContainer);
            Cache.OeQueryCache queryCache = GetQueryCache(dataAdapter);
            int count = queryCache.CacheCount;

            foreach (IEdmModel refModel in edmModel.ReferencedModels)
                if (refModel.EntityContainer != null)
                    count += GetQueryCacheCount(refModel);

            return queryCache.AllowCache ? count : -1;
        }
        private static IList ToOpenType(IEnumerable entities, IReadOnlyList<EfInclude> includes)
        {
            return new OpenTypeConverter(includes).Convert(entities);
        }
    }
}
