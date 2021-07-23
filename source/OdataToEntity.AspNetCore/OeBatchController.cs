using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.OData;
using Microsoft.OData.Edm;
using OdataToEntity.Db;
using OdataToEntity.Parsers;
using OdataToEntity.Writers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity.AspNetCore
{
    public class OeBatchController : ControllerBase
    {
        [HttpPost]
        public Task Batch()
        {
            return BatchCore();
        }
        [HttpPost]
        protected virtual async Task BatchCore()
        {
            String basePath = "";
            if (base.HttpContext.Request.PathBase.HasValue)
                basePath = base.HttpContext.Request.PathBase;
            else
            {
                if (base.HttpContext.Request.Path.Value != null)
                {
                    int i = base.HttpContext.Request.Path.Value.IndexOf('/', 1);
                    if (i > 0)
                        basePath = base.HttpContext.Request.Path.Value.Substring(0, i);
                }
            }
            Uri baseUri = UriHelper.GetBaseUri(base.Request);

            OeBatchMessage batchMessage = await OeBatchMessage.CreateBatchMessageAsync(EdmModel, baseUri,
                base.HttpContext.Request.Body, base.HttpContext.Request.ContentType, OeParser.ServiceProvider);
            if (batchMessage.Changeset == null)
                return;

            OeDataAdapter? dataAdapter = null;
            Object? dataContext = null;
            ActionDescriptorCollection actionDescriptors = GetService<IActionDescriptorCollectionProvider>().ActionDescriptors;
            IActionInvokerFactory actionInvokerFactory = GetService<IActionInvokerFactory>();
            try
            {
                IEdmModel? refModel = null;
                foreach (OeOperationMessage operation in batchMessage.Changeset)
                {
                    if (dataContext == null)
                    {
                        refModel = EdmModel.GetEdmModel(operation.EntitySet.Container);
                        dataAdapter = refModel.GetDataAdapter(operation.EntitySet.Container);
                        dataContext = dataAdapter.CreateDataContext();
                    }

                    OeEntitySetAdapter entitySetAdapter = refModel!.GetEntitySetAdapter(operation.EntitySet);
                    Object entity = OeDataContext.CreateEntity(operation, entitySetAdapter.EntityType);
                    if (operation.Method == ODataConstants.MethodPatch)
                        base.HttpContext.Request.Method = HttpMethods.Patch;

                    var oeDataContext = new OeDataContext(entitySetAdapter, refModel!, dataContext, operation);
                    var modelState = new OeBatchFilterAttributeAttribute.BatchModelStateDictionary(dataAdapter!, oeDataContext, entity);
                    OnBeforeInvokeController(modelState.DataContext, operation.Entry);

                    String path = basePath + "/" + entitySetAdapter.EntitySetName;
                    List<ActionDescriptor> candidates = OeRouter.SelectCandidates(actionDescriptors.Items, base.HttpContext, base.RouteData.Values, path, operation.Method);
                    if (candidates.Count > 1)
                        throw new InvalidOperationException("Ambiguous action " + String.Join(Environment.NewLine, candidates.Select(c => c.DisplayName)));
                    if (candidates.Count == 0)
                        throw new InvalidOperationException("Action " + operation.Method + " for controller " + basePath + " not found");

                    var actionContext = new ActionContext(base.HttpContext, base.HttpContext.GetRouteData(), candidates[0], modelState);
                    IActionInvoker actionInvoker = actionInvokerFactory.CreateInvoker(actionContext);
                    await actionInvoker.InvokeAsync().ConfigureAwait(false);
                }

                if (dataAdapter != null && dataContext != null)
                {
                    await SaveChangesAsync(dataContext).ConfigureAwait(false);
                    foreach (OeOperationMessage operation in batchMessage.Changeset)
                        dataAdapter.EntitySetAdapters.Find(operation.EntitySet).UpdateEntityAfterSave(dataContext, operation.Entry);
                }
            }
            finally
            {
                if (dataAdapter != null && dataContext != null)
                    dataAdapter.CloseDataContext(dataContext);
            }

            base.HttpContext.Response.ContentType = base.HttpContext.Request.ContentType;
            var batchWriter = new OeBatchWriter(EdmModel, baseUri);
            await batchWriter.WriteBatchAsync(base.HttpContext.Response.Body, batchMessage);
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
        protected virtual void OnBeforeInvokeController(OeDataContext dataContext, ODataResource entry)
        {
        }
        private T GetService<T>()
        {
            return (T)(base.HttpContext.RequestServices.GetService(typeof(T))
                ?? throw new InvalidOperationException("Type " + typeof(T).FullName + " not register in HttpContext.RequestServices"));
        }
        protected virtual async Task<int> SaveChangesAsync(Object dataContext)
        {
            OeDataAdapter dataAdapter = EdmModel.GetDataAdapter(dataContext.GetType());
            return await dataAdapter.SaveChangesAsync(dataContext, CancellationToken.None).ConfigureAwait(false);
        }
        protected async Task SaveWithoutController()
        {
            base.HttpContext.Response.ContentType = base.HttpContext.Request.ContentType;

            var parser = new OeBatchParser(UriHelper.GetBaseUri(base.Request), EdmModel);
            await parser.ExecuteAsync(base.HttpContext.Request.Body, base.HttpContext.Response.Body,
                base.HttpContext.Request.ContentType, CancellationToken.None).ConfigureAwait(false);
        }

        protected IEdmModel EdmModel => base.HttpContext.GetEdmModel();
    }
}
