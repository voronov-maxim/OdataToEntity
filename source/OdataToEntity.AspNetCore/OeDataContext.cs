using Microsoft.OData;
using Microsoft.OData.Edm;
using OdataToEntity.Db;
using OdataToEntity.Parsers;
using System;
using System.Linq;
using System.Reflection;

namespace OdataToEntity.AspNetCore
{
    public sealed class OeDataContext
    {
        private readonly OeEntitySetAdapter _entitySetAdapter;
        private readonly OeOperationMessage _operation;

        public OeDataContext()
        {
        }

        public OeDataContext(ref OeEntitySetAdapter entitySetAdapter, IEdmModel edmModel, Object dataContext, OeOperationMessage operation)
        {
            _entitySetAdapter = entitySetAdapter;
            DataContext = dataContext;
            EdmModel = edmModel;
            _operation = operation;
        }

        internal static ODataResource CreateEntry(Object entity, PropertyInfo[] structuralProperties)
        {
            Type clrEntityType = entity.GetType();
            var odataProperties = new ODataProperty[structuralProperties.Length];
            for (int i = 0; i < odataProperties.Length; i++)
            {
                Object value = structuralProperties[i].GetValue(entity);
                ODataValue odataValue = null;
                if (value == null)
                    odataValue = new ODataNullValue();
                else if (value.GetType().IsEnum)
                    odataValue = new ODataEnumValue(value.ToString());
                else if (value is DateTime dateTime)
                {
                    switch (dateTime.Kind)
                    {
                        case DateTimeKind.Unspecified:
                            value = new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc));
                            break;
                        case DateTimeKind.Utc:
                            value = new DateTimeOffset(dateTime);
                            break;
                        case DateTimeKind.Local:
                            value = new DateTimeOffset(dateTime.ToUniversalTime());
                            break;
                        default:
                            throw new ArgumentOutOfRangeException("unknown DateTimeKind " + dateTime.Kind.ToString());
                    }
                    odataValue = new ODataPrimitiveValue(value);
                }
                else
                    odataValue = new ODataPrimitiveValue(value);

                odataProperties[i] = new ODataProperty() { Name = structuralProperties[i].Name, Value = odataValue };
            }

            return new ODataResource
            {
                TypeName = clrEntityType.FullName,
                Properties = odataProperties
            };
        }
        private ODataResource CreateEntry(Object entity)
        {
            IEdmEntitySet entitySet = EdmModel.FindDeclaredEntitySet(_entitySetAdapter.EntitySetMetaAdapter.EntitySetName);
            PropertyInfo[] structuralProperties = entitySet.EntityType().StructuralProperties().Select(p => _entitySetAdapter.EntityType.GetProperty(p.Name)).ToArray();
            return CreateEntry(entity, structuralProperties);
        }
        public void Update(Object entity)
        {
            ODataResource entry = CreateEntry(entity);
            switch (_operation.Method)
            {
                case ODataConstants.MethodDelete:
                    _entitySetAdapter.RemoveEntity(DataContext, entry);
                    break;
                case ODataConstants.MethodPatch:
                    _entitySetAdapter.AttachEntity(DataContext, entry);
                    break;
                case ODataConstants.MethodPost:
                    _entitySetAdapter.AddEntity(DataContext, entry);
                    break;
                default:
                    throw new NotImplementedException(_operation.Method);
            }
        }

        public Object DataContext { get; }
        public IEdmModel EdmModel { get; }
        public String HttpMethod => _operation?.Method;
    }
}
