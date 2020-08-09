using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Template;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OdataToEntity.AspNetCore
{
    public sealed class OeRouter : IRouter
    {
        private readonly ActionDescriptorCollection _actionDescriptors;
        private readonly IActionInvokerFactory _actionInvokerFactory;

        public OeRouter(IActionInvokerFactory actionInvokerFactory, IActionDescriptorCollectionProvider actionDescriptorCollectionProvider)
        {
            _actionInvokerFactory = actionInvokerFactory;
            _actionDescriptors = actionDescriptorCollectionProvider.ActionDescriptors;
        }

        private static bool ActionConstaint(ActionDescriptor actionDescriptor, HttpContext httpContext, RouteValueDictionary values, String httpMethod)
        {
            if (actionDescriptor.ActionConstraints == null)
                if (actionDescriptor is ControllerActionDescriptor controllerActionDescriptor)
                    return String.Compare(controllerActionDescriptor.ActionName, httpMethod, StringComparison.OrdinalIgnoreCase) == 0;
                else
                    return false;

            var candidate = new ActionSelectorCandidate(actionDescriptor, null);
            var constraintContext = new ActionConstraintContext() { CurrentCandidate = candidate };
            if (constraintContext.RouteContext == null)
                constraintContext.RouteContext = new RouteContext(httpContext) { RouteData = new RouteData(values) };

            for (int i = 0; i < actionDescriptor.ActionConstraints.Count; i++)
                if (actionDescriptor.ActionConstraints[i] is IActionConstraint actionConstraint && actionConstraint.Accept(constraintContext))
                    return true;

            return false;
        }
        private static String? GetPath(String? path)
        {
            if (path == null)
                return null;

            int pos1, pos2;

            if ((pos2 = path.IndexOf(")/", StringComparison.Ordinal)) != -1)
            {
                pos1 = path.LastIndexOf('(', pos2);
                if (pos1 == -1)
                    return null;

                return path.Substring(0, pos1) + "/" + path.Substring(pos1 + 1, pos2 - pos1 - 1) + path.Substring(pos2 + 1);
            }

            if (path[path.Length - 1] == ')')
            {
                pos1 = path.LastIndexOf('(');
                if (pos1 == -1)
                    return null;

                return path.Substring(0, pos1) + "/" + path.Substring(pos1 + 1, path.Length - pos1 - 2);
            }

            if ((pos1 = path.IndexOf("/$", StringComparison.Ordinal)) != -1)
                return path.Substring(0, pos1);

            return path;
        }
        public VirtualPathData GetVirtualPath(VirtualPathContext context)
        {
            throw new NotSupportedException();
        }
        public Task RouteAsync(RouteContext context)
        {
            String? path = GetPath(context.HttpContext.Request.Path.Value);
            if (path == null)
                throw new InvalidOperationException("Cannot resolve path " + context.HttpContext.Request.Path.Value);

            IReadOnlyList<ActionDescriptor> candidates = SelectCandidates(_actionDescriptors.Items,
                context.HttpContext, context.RouteData.Values, path, context.HttpContext.Request.Method);
            if (candidates.Count == 0)
                return Task.CompletedTask;
            if (candidates.Count > 1)
                throw new InvalidOperationException("Ambiguous action " + path + " " + String.Join(";", candidates.Select(c => c.DisplayName)));

            context.Handler = async ctx =>
            {
                var actionContext = new ActionContext(context.HttpContext, ctx.GetRouteData(), candidates[0]);
                IActionInvoker actionInvoker = _actionInvokerFactory.CreateInvoker(actionContext);
                if (actionInvoker == null)
                    return;

                await actionInvoker.InvokeAsync().ConfigureAwait(false);
            };

            return Task.CompletedTask;
        }
        internal static List<ActionDescriptor> SelectCandidates(IReadOnlyList<ActionDescriptor> actionDescriptors,
            HttpContext httpContext, RouteValueDictionary values, String path, String httpMethod)
        {
            var selectCandidates = new List<ActionDescriptor>();
            for (int i = 0; i < actionDescriptors.Count; i++)
            {
                ActionDescriptor actionDescriptor = actionDescriptors[i];
                if (actionDescriptor.AttributeRouteInfo != null)
                {
                    RouteTemplate template = TemplateParser.Parse(actionDescriptor.AttributeRouteInfo.Template);
                    var matcher = new TemplateMatcher(template, new RouteValueDictionary());
                    if (matcher.TryMatch(path, values) && ActionConstaint(actionDescriptor, httpContext, values, httpMethod))
                        selectCandidates.Add(actionDescriptor);
                }
            }

            if (selectCandidates.Count == 0)
            {
                if (HttpMethods.IsGet(httpMethod))
                {
                    for (int i = 0; i < actionDescriptors.Count; i++)
                    {
                        ActionDescriptor actionDescriptor = actionDescriptors[i];
                        if (actionDescriptor.AttributeRouteInfo != null && path != null
                            && path.IndexOf(actionDescriptor.AttributeRouteInfo.Template, StringComparison.OrdinalIgnoreCase) == 1
                            && path[actionDescriptor.AttributeRouteInfo.Template.Length + 1] == '/'
                            && ActionConstaint(actionDescriptor, httpContext, values, httpMethod))
                        {
                            selectCandidates.Add(actionDescriptor);
                            break;
                        }
                    }
                    return selectCandidates;
                }

                if (HttpMethods.IsPatch(httpMethod) || HttpMethods.IsDelete(httpMethod) || HttpMethods.IsPut(httpMethod))
                    return SelectCandidates(actionDescriptors, httpContext, values, path, HttpMethods.Post);
            }

            return selectCandidates;
        }
    }
}
