using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.OData.Edm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OdataToEntity.Test.AspMvcServer
{
    public sealed class OeRouter : IRouter
    {
        private readonly IActionDescriptorCollectionProvider _actionDescriptorCollectionProvider;
        private readonly IActionInvokerFactory _actionInvokerFactory;
        private readonly IActionSelector _actionSelector;
        private readonly Dictionary<String, IReadOnlyList<ActionDescriptor>> _operationDescriptors;
        private readonly Dictionary<String, IEdmOperationImport> _operationImports;

        public OeRouter(IActionInvokerFactory actionInvokerFactory, IActionSelector actionSelector, IActionDescriptorCollectionProvider actionDescriptorCollectionProvider, IEdmModel edmModel)
        {
            _actionInvokerFactory = actionInvokerFactory;
            _actionSelector = actionSelector;
            _actionDescriptorCollectionProvider = actionDescriptorCollectionProvider;

            _operationDescriptors = GetOperationDescriptors(actionDescriptorCollectionProvider.ActionDescriptors.Items);
            _operationImports = edmModel.EntityContainer.OperationImports().ToDictionary(o => o.Name);
        }

        private static Dictionary<String, IReadOnlyList<ActionDescriptor>> GetOperationDescriptors(IReadOnlyList<ActionDescriptor> actionDescriptors)
        {
            return actionDescriptors.Cast<ControllerActionDescriptor>().GroupBy(o => o.ActionName).
                ToDictionary(g => g.First().ControllerName + "." + g.Key, g => (IReadOnlyList<ActionDescriptor>)g.ToList<ActionDescriptor>());
        }
        private IReadOnlyList<ActionDescriptor> GetOperationCadidates(String[] segments)
        {
            IEdmOperationImport operationImport;
            if (!_operationImports.TryGetValue(segments.Last(), out operationImport))
                return Array.Empty<ActionDescriptor>();

            IReadOnlyList<ActionDescriptor> candidates;
            if (!_operationDescriptors.TryGetValue(operationImport.Operation.Name, out candidates))
                return Array.Empty<ActionDescriptor>();

            return candidates;
        }
        private static String GetPath(String path)
        {
            int pos;
            if (path.EndsWith(')'))
            {
                pos = path.LastIndexOf('(');
                if (pos == -1)
                    return null;
            }
            else if ((pos = path.IndexOf(")/")) != -1)
            {
                pos = path.LastIndexOf('(', pos);
                if (pos == -1)
                    return null;
            }
            else if ((pos = path.IndexOf("/$")) != -1)
            {
            }
            else
                return null;

            return path.Substring(0, pos);
        }
        public VirtualPathData GetVirtualPath(VirtualPathContext context)
        {
            throw new NotSupportedException();
        }
        public Task RouteAsync(RouteContext context)
        {
            IReadOnlyList<ActionDescriptor> candidates = _actionSelector.SelectCandidates(context);
            if (candidates.Count == 0)
            {
                String path = GetPath(context.HttpContext.Request.Path.Value);
                if (path == null)
                {
                    path = context.HttpContext.Request.Path.Value;
                    String[] segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
                    candidates = GetOperationCadidates(segments);

                    int length = path.Length - segments.Last().Length -2;
                    if (length > 0)
                        candidates = candidates.Where(c => String.Compare(c.AttributeRouteInfo.Template, 0, path, 1, length, StringComparison.OrdinalIgnoreCase) == 0).ToList();
                }
                else
                {
                    String[] segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
                    candidates = SelectCandidates(_actionDescriptorCollectionProvider.ActionDescriptors.Items, segments);
                    if (candidates.Count == 0)
                        candidates = GetOperationCadidates(segments);
                }
                if (candidates.Count == 0)
                    return Task.CompletedTask;
            }

            ActionDescriptor actionDescriptor = _actionSelector.SelectBestCandidate(context, candidates);
            if (actionDescriptor == null)
                return Task.CompletedTask;

            context.Handler = ctx =>
            {
                var actionContext = new ActionContext(context.HttpContext, ctx.GetRouteData(), actionDescriptor);
                IActionInvoker actionIinvoker = _actionInvokerFactory.CreateInvoker(actionContext);
                if (actionIinvoker == null)
                    return Task.CompletedTask;

                return actionIinvoker.InvokeAsync();
            };

            return Task.CompletedTask;
        }
        private static List<ActionDescriptor> SelectCandidates(IReadOnlyList<ActionDescriptor> actionDescriptors, String[] segments)
        {
            var selectCandidates = new List<ActionDescriptor>();
            for (int i = 0; i < actionDescriptors.Count; i++)
            {
                ActionDescriptor actionDescriptor = actionDescriptors[i];
                if (actionDescriptor.AttributeRouteInfo == null)
                {
                    String value;
                    actionDescriptor.RouteValues.TryGetValue("controller", out value);
                    if (String.Compare(value, segments[0]) == 0)
                        selectCandidates.Add(actionDescriptor);
                }
                else
                {
                    String[] controllerSegments = actionDescriptor.AttributeRouteInfo.Template.Split('/');
                    if (controllerSegments.Length != segments.Length)
                        continue;

                    bool match = false;
                    for (int j = 0; j < controllerSegments.Length; j++)
                    {
                        match = String.Compare(controllerSegments[j], segments[j], StringComparison.OrdinalIgnoreCase) == 0;
                        if (!match)
                            break;
                    }
                    if (match)
                        selectCandidates.Add(actionDescriptor);
                }
            }

            return selectCandidates;
        }
    }
}
