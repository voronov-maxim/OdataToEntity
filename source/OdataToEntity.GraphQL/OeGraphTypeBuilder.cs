using GraphQL;
using GraphQL.Resolvers;
using GraphQL.Types;
using Microsoft.OData.Edm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace OdataToEntity.GraphQL
{
    public readonly struct OeGraphTypeBuilder
    {
        private sealed class FieldResolver : IFieldResolver
        {
            private readonly PropertyInfo _propertyInfo;

            public FieldResolver(PropertyInfo propertyInfo)
            {
                _propertyInfo = propertyInfo;
            }

            public Object Resolve(ResolveFieldContext context)
            {
                if (context.Source is IDictionary<String, Object> dictionary)
                    return dictionary[_propertyInfo.Name];

                return _propertyInfo.GetValue(context.Source);
            }
        }

        private readonly IEdmModel _edmModel;
        private readonly Dictionary<Type, IGraphType> _clrTypeToObjectGraphType;

        public OeGraphTypeBuilder(IEdmModel edmModel)
        {
            _edmModel = edmModel;
            _clrTypeToObjectGraphType = new Dictionary<Type, IGraphType>();
        }

        public void AddNavigationProperties(FieldType fieldType)
        {
            Type entityType = GetEntityTypeFromResolvedType(((ListGraphType)fieldType.ResolvedType).ResolvedType);
            var entityGraphType = (IObjectGraphType)_clrTypeToObjectGraphType[entityType];

            foreach (PropertyInfo propertyInfo in entityType.GetProperties())
                if (!entityGraphType.HasField(propertyInfo.Name))
                {
                    IGraphType resolvedType;
                    QueryArgument[] queryArguments;
                    Type itemType = Parsers.OeExpressionHelper.GetCollectionItemType(propertyInfo.PropertyType);
                    if (itemType == null)
                    {
                        resolvedType = _clrTypeToObjectGraphType[propertyInfo.PropertyType];
                        queryArguments = CreateQueryArguments(propertyInfo.PropertyType, true);
                    }
                    else
                    {
                        resolvedType = new ListGraphType(_clrTypeToObjectGraphType[itemType]);
                        queryArguments = CreateQueryArguments(itemType, true);
                    }

                    if (IsRequired(propertyInfo))
                        resolvedType = new NonNullGraphType(resolvedType);

                    var entityFieldType = new FieldType()
                    {
                        Arguments = new QueryArguments(queryArguments),
                        Name = propertyInfo.Name,
                        ResolvedType = resolvedType,
                        Resolver = new FieldResolver(propertyInfo),
                    };
                    entityGraphType.AddField(entityFieldType);
                }

            fieldType.Arguments = new QueryArguments(CreateQueryArguments(entityType, false));
        }
        private QueryArgument[] CreateQueryArguments(Type entityType, bool onlyStructural)
        {
            PropertyInfo[] properties = entityType.GetProperties();
            if (onlyStructural)
                properties = properties.Where(p => p.PropertyType.IsValueType || p.PropertyType == typeof(String)).ToArray();

            var queryArguments = new QueryArgument[properties.Length];
            for (int i = 0; i < properties.Length; i++)
            {
                QueryArgument queryArgument;
                var entityGraphType = (IObjectGraphType)_clrTypeToObjectGraphType[entityType];
                FieldType fieldType = entityGraphType.Fields.Single(f => f.Name == properties[i].Name);
                if (fieldType.Type == null)
                {
                    IGraphType resolvedType = fieldType.ResolvedType;
                    if (resolvedType is NonNullGraphType nonNullGraphType)
                        resolvedType = nonNullGraphType.ResolvedType;
                    queryArgument = new QueryArgument(resolvedType.GetType()) { ResolvedType = resolvedType };
                }
                else
                {
                    if (fieldType.Type.IsGenericType && typeof(NonNullGraphType).IsAssignableFrom(fieldType.Type))
                        queryArgument = new QueryArgument(fieldType.Type.GetGenericArguments()[0]);
                    else
                        queryArgument = new QueryArgument(fieldType.Type);
                }
                queryArgument.Name = NameFirstCharLower(properties[i].Name);
                queryArguments[i] = queryArgument;
            }
            return queryArguments;
        }
        private IObjectGraphType CreateGraphType(Type entityType)
        {
            if (_clrTypeToObjectGraphType.TryGetValue(entityType, out IGraphType graphType))
                return (IObjectGraphType)graphType;

            var objectGraphType = (IObjectGraphType)graphType;
            Type objectGraphTypeType = typeof(ObjectGraphType<>).MakeGenericType(entityType);
            objectGraphType = (IObjectGraphType)Activator.CreateInstance(objectGraphTypeType);
            objectGraphType.Name = NameFirstCharLower(entityType.Name);
            objectGraphType.IsTypeOf = t => t is IDictionary<String, Object>;

            foreach (PropertyInfo propertyInfo in entityType.GetProperties())
                if (propertyInfo.PropertyType.IsValueType || propertyInfo.PropertyType == typeof(String))
                    objectGraphType.AddField(CreateStructuralFieldType(propertyInfo));

            _clrTypeToObjectGraphType.Add(entityType, objectGraphType);
            return objectGraphType;
        }
        public ListGraphType CreateListGraphType(Type entityType)
        {
            return new ListGraphType(CreateGraphType(entityType));
        }
        private FieldType CreateStructuralFieldType(PropertyInfo propertyInfo)
        {
            Type graphType;
            bool isNullable = !IsRequired(propertyInfo);
            Type enumType = propertyInfo.PropertyType;
            if (enumType.IsEnum || ((enumType = Nullable.GetUnderlyingType(enumType)) != null && enumType.IsEnum))
            {
                graphType = typeof(EnumerationGraphType<>).MakeGenericType(enumType);
                if (!isNullable)
                    graphType = typeof(NonNullGraphType<>).MakeGenericType(graphType);
            }
            else
            {
                if (IsKey(propertyInfo))
                    graphType = typeof(IdGraphType);
                else
                {
                    if (propertyInfo.PropertyType == typeof(DateTimeOffset) || propertyInfo.PropertyType == typeof(DateTimeOffset?))
                        graphType = typeof(DateTime).GetGraphTypeFromType(isNullable);
                    else
                        graphType = propertyInfo.PropertyType.GetGraphTypeFromType(isNullable);
                }
            }

            var fieldType = new FieldType()
            {
                Name = propertyInfo.Name,
                Type = graphType,
                Resolver = new FieldResolver(propertyInfo),
            };

            return fieldType;
        }
        private Type GetEntityTypeFromResolvedType(IGraphType resolvedType)
        {
            return resolvedType.GetType().GetGenericArguments()[0];
        }
        private bool IsKey(PropertyInfo propertyInfo)
        {
            var entityType = (IEdmEntityType)_edmModel.FindDeclaredType(propertyInfo.DeclaringType.FullName);
            foreach (IEdmStructuralProperty key in entityType.Key())
                if (String.Compare(key.Name, propertyInfo.Name, StringComparison.OrdinalIgnoreCase) == 0)
                    return true;
            return false;
        }
        private bool IsRequired(PropertyInfo propertyInfo)
        {
            var entityType = (IEdmEntityType)_edmModel.FindDeclaredType(propertyInfo.DeclaringType.FullName);
            IEdmProperty edmProperty = entityType.FindProperty(propertyInfo.Name);
            return !edmProperty.Type.IsNullable;
        }
        private static String NameFirstCharLower(String name)
        {
            if (Char.IsUpper(name, 0))
                return Char.ToLowerInvariant(name[0]).ToString() + name.Substring(1);
            return name;
        }
    }
}
