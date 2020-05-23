using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.OData;
using Microsoft.OData.Edm;
using OdataToEntity.Db;
using OdataToEntity.Parsers;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Permissions;
using System.Threading.Tasks;

namespace OdataToEntity.AspNetCore
{
    internal sealed class OeDataContextBinder : IModelBinder
    {
        public async Task BindModelAsync(ModelBindingContext bindingContext)
        {
            if (bindingContext.ModelState is OeBatchFilterAttributeAttribute.BatchModelStateDictionary batchModelState)
                bindingContext.Result = ModelBindingResult.Success(batchModelState.DataContext); //batch operation
            else
                bindingContext.Result = ModelBindingResult.Success(await GetModelStateAsync(bindingContext.HttpContext)); //single operation
        }
        private static async Task<OeBatchFilterAttributeAttribute.BatchModelStateDictionary> GetModelStateAsync(HttpContext httpContext)
        {
            var edmModel = (IEdmModel)httpContext.RequestServices.GetService(typeof(IEdmModel));
            Uri baseUri = UriHelper.GetBaseUri(httpContext.Request);
            Uri requestUri = UriHelper.GetUri(httpContext.Request);
            OeOperationMessage operation = await OeBatchMessage.CreateOperationMessageAsync(edmModel, baseUri, requestUri,
                httpContext.Request.Body, httpContext.Request.ContentType, httpContext.Request.Method, OeParser.ServiceProvider).ConfigureAwait(false);

            IEdmModel refModel = edmModel.GetEdmModel(operation.EntitySet.Container);
            OeDataAdapter dataAdapter = refModel.GetDataAdapter(operation.EntitySet.Container);
            Object dataContext = dataAdapter.CreateDataContext();
            OeEntitySetAdapter entitySetAdapter = refModel.GetEntitySetAdapter(operation.EntitySet);
            Object entity = OeDataContext.CreateEntity(operation, entitySetAdapter.EntityType);

            var oeDataContext = new OeDataContext(entitySetAdapter, refModel, dataContext, operation);
            return new OeBatchFilterAttributeAttribute.BatchModelStateDictionary(dataAdapter, oeDataContext, entity);
        }
    }

    [ModelBinder(BinderType = typeof(OeDataContextBinder))]
    public sealed class OeDataContext
    {
        private readonly OeEntitySetAdapter _entitySetAdapter;

        public OeDataContext(OeEntitySetAdapter entitySetAdapter, IEdmModel edmModel, Object dataContext, in OeOperationMessage operation)
        {
            _entitySetAdapter = entitySetAdapter;
            DbContext = dataContext;
            EdmModel = edmModel;
            Operation = operation;
        }

        internal static Object CreateEntity(OeOperationMessage operation, Type clrEntityType)
        {
            if (operation.Method == ODataConstants.MethodPatch)
            {
                var properties = new Dictionary<String, Object>();
                foreach (ODataProperty odataProperty in operation.Entry.Properties)
                {
                    PropertyInfo? propertyInfo = clrEntityType.GetProperty(odataProperty.Name);
                    if (propertyInfo == null)
                        throw new InvalidOperationException("Not found property " + odataProperty.Name + " in type " + clrEntityType.FullName);

                    properties[odataProperty.Name] = OeEdmClrHelper.GetClrValue(propertyInfo.PropertyType, odataProperty.Value);
                }
                return properties;
            }

            return OeEdmClrHelper.CreateEntity(clrEntityType, operation.Entry);
        }
        private ODataResource CreateEntry(Object entity)
        {
            IEdmEntitySet entitySet = OeEdmClrHelper.GetEntitySet(EdmModel, _entitySetAdapter.EntitySetName);
            var structuralProperties = new List<PropertyInfo>();
            foreach (IEdmStructuralProperty structuralProperty in entitySet.EntityType().StructuralProperties())
            {
                PropertyInfo? propertyInfo = _entitySetAdapter.EntityType.GetProperty(structuralProperty.Name);
                if (propertyInfo == null)
                    throw new InvalidOperationException("Not found property " + structuralProperty.Name + " in type " + _entitySetAdapter.EntityType.FullName);
                structuralProperties.Add(propertyInfo);
            }
            return CreateEntry(entity, structuralProperties);
        }
        private ODataResource CreateEntry(IDictionary<String, Object> entityProperties)
        {
            var odataProperties = new ODataProperty[entityProperties.Count];
            int i = 0;
            foreach (KeyValuePair<String, Object> entityProperty in entityProperties)
            {
                ODataValue odataValue = OeEdmClrHelper.CreateODataValue(entityProperty.Value);
                odataProperties[i++] = new ODataProperty() { Name = entityProperty.Key, Value = odataValue };
            }

            return new ODataResource
            {
                TypeName = _entitySetAdapter.EntityType.FullName,
                Properties = odataProperties
            };
        }
        private static ODataResource CreateEntry(Object entity, List<PropertyInfo> structuralProperties)
        {
            Type clrEntityType = entity.GetType();
            var odataProperties = new ODataProperty[structuralProperties.Count];
            for (int i = 0; i < odataProperties.Length; i++)
            {
                ODataValue odataValue = OeEdmClrHelper.CreateODataValue(structuralProperties[i].GetValue(entity));
                odataProperties[i] = new ODataProperty() { Name = structuralProperties[i].Name, Value = odataValue };
            }

            return new ODataResource
            {
                TypeName = clrEntityType.FullName,
                Properties = odataProperties
            };
        }
        public void Update(Object entity)
        {
            ODataResource entry;
            switch (Operation.Method)
            {
                case ODataConstants.MethodDelete:
                    entry = CreateEntry(entity);
                    _entitySetAdapter.RemoveEntity(DbContext, entry);
                    return;
                case ODataConstants.MethodPatch:
                    entry = CreateEntry((IDictionary<String, Object>)entity);
                    _entitySetAdapter.AttachEntity(DbContext, entry);
                    break;
                case ODataConstants.MethodPost:
                    entry = CreateEntry(entity);
                    _entitySetAdapter.AddEntity(DbContext, entry);
                    break;
                default:
                    throw new NotImplementedException(Operation.Method);
            }

            Operation.Entry.Properties = entry.Properties;
            Operation.Entry.InstanceAnnotations = entry.InstanceAnnotations;
        }

        public Object DbContext { get; }
        public IEdmModel EdmModel { get; }
        internal OeOperationMessage Operation { get; }
        public String HttpMethod => Operation.Method;
    }
}
