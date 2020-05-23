using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using OdataToEntity.Parsers;
using System;
using System.Collections.Generic;
using System.Threading;

namespace OdataToEntity.AspNetCore
{
    public sealed class OeBatchFilterConvention : IActionModelConvention
    {
        public void Apply(ActionModel action)
        {
            for (int i = 0; i < action.Parameters.Count; i++)
                if (action.Parameters[i].ParameterType == typeof(OeDataContext))
                {
                    action.Filters.Add(new OeBatchFilterAttributeAttribute());
                    return;
                }
        }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class OeBatchFilterAttributeAttribute : Attribute, IActionFilter
    {
        internal sealed class BatchModelStateDictionary : ModelStateDictionary
        {
            public BatchModelStateDictionary(Db.OeDataAdapter dataAdapter, OeDataContext dataContext, Object entity)
            {
                DataAdapter = dataAdapter;
                DataContext = dataContext;
                Entity = entity;
            }

            public Db.OeDataAdapter DataAdapter { get; }
            public OeDataContext DataContext { get; }
            public Object Entity { get; }
        }

        private readonly static AsyncLocal<ModelStateDictionary> _asyncLocal = new AsyncLocal<ModelStateDictionary>();

        public void OnActionExecuted(ActionExecutedContext context)
        {
            if (!(context.ModelState is BatchModelStateDictionary) && _asyncLocal.Value is BatchModelStateDictionary modelState)
            {
                //single operation
                _asyncLocal.Value = new ModelStateDictionary();
                Db.OeDataAdapter dataAdapter = modelState.DataAdapter;
                Object dataContext = modelState.DataContext.DbContext;
                OeOperationMessage operation = modelState.DataContext.Operation;
                try
                {
                    dataAdapter.SaveChangesAsync(dataContext, CancellationToken.None).GetAwaiter().GetResult();
                    dataAdapter.EntitySetAdapters.Find(operation.EntitySet).UpdateEntityAfterSave(dataContext, operation.Entry);

                    context.HttpContext.Response.ContentType = context.HttpContext.Request.ContentType;
                    var batchWriter = new Writers.OeBatchWriter(modelState.DataContext.EdmModel, UriHelper.GetBaseUri(context.HttpContext.Request));
                    batchWriter.WriteOperationAsync(context.HttpContext.Response.Body, operation).GetAwaiter().GetResult();
                }
                finally
                {
                    dataAdapter.CloseDataContext(dataContext);
                }
            }
        }
        public void OnActionExecuting(ActionExecutingContext context)
        {
            BatchModelStateDictionary? modelState = null;
            if (context.ModelState is BatchModelStateDictionary batchModelState)
                modelState = batchModelState; //batch operation
            else
            {
                //single operation
                foreach (var keyValue in context.ActionArguments)
                    if (keyValue.Value is BatchModelStateDictionary batchModelState2)
                    {
                        modelState = batchModelState2;
                        _asyncLocal.Value = batchModelState2;
                        context.ActionArguments[keyValue.Key] = batchModelState2.DataContext;
                        break;
                    }

                if (modelState == null)
                    throw new InvalidOperationException("BatchModelStateDictionary");
            }

            foreach (KeyValuePair<String, Object> keyValue in context.ActionArguments)
                if (keyValue.Value.GetType().IsAssignableFrom(modelState.Entity.GetType()))
                {
                    context.ActionArguments[keyValue.Key] = modelState.Entity;
                    break;
                }
        }
    }
}
