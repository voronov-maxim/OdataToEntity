using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System;
using System.Collections.Generic;

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

    public sealed class OeBatchFilterAttributeAttribute : Attribute, IActionFilter
    {
        internal sealed class BatchModelStateDictionary : ModelStateDictionary
        {
            public Object Entity { get; set; }
            public OeDataContext DataContext { get; set; }
        }

        public void OnActionExecuted(ActionExecutedContext context)
        {
        }
        public void OnActionExecuting(ActionExecutingContext context)
        {
            if (context.ModelState is BatchModelStateDictionary batchModelState)
            {
                String dataContextName = null;
                String entityName = null;
                foreach (KeyValuePair<String, Object> keyValue in context.ActionArguments)
                    if (keyValue.Value is OeDataContext)
                        dataContextName = keyValue.Key;
                    else if (keyValue.Value.GetType().IsAssignableFrom(batchModelState.Entity.GetType()))
                        entityName = keyValue.Key;

                if (dataContextName != null)
                    context.ActionArguments[dataContextName] = batchModelState.DataContext;
                if (entityName != null)
                    context.ActionArguments[entityName] = batchModelState.Entity;
            }
        }
    }
}
