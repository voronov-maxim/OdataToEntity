using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System;
using System.Linq;

namespace OdataToEntity.AspNetCore
{
    public sealed class OeFilterAttribute : ActionFilterAttribute
    {
        internal sealed class BatchModelStateDictionary : ModelStateDictionary
        {
            public Object Entity { get; set; }
            public OeDataContext DataContext { get; set; }
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (context.ModelState is BatchModelStateDictionary batchModelState)
            {
                String parameterName;

                parameterName = context.ActionArguments.SingleOrDefault(a => a.Value is OeDataContext).Key;
                if (parameterName != null)
                    context.ActionArguments[parameterName] = batchModelState.DataContext;

                parameterName = context.ActionArguments.SingleOrDefault(a => a.Value.GetType() == batchModelState.Entity.GetType()).Key;
                if (parameterName != null)
                    context.ActionArguments[parameterName] = batchModelState.Entity;
            }
        }
    }
}
