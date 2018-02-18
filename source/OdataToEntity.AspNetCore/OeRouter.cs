using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Internal;
using Microsoft.AspNetCore.Routing;
using Microsoft.OData.Edm;
using OdataToEntity.Db;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OdataToEntity.AspNetCore
{
    public sealed class OeRouter : IRouter
    {
        private readonly IActionDescriptorCollectionProvider _actionDescriptorCollectionProvider;
        private readonly IActionInvokerFactory _actionInvokerFactory;
        private readonly IActionSelector _actionSelector;
        private readonly OeDataAdapter _dataAdapter;
        private readonly IEdmModel _edmModel;
        private readonly Dictionary<String, IReadOnlyList<ActionDescriptor>> _operationDescriptors;
        private readonly Dictionary<String, IEdmOperationImport> _operationImports;

        public OeRouter(IActionInvokerFactory actionInvokerFactory, IActionSelector actionSelector, IActionDescriptorCollectionProvider actionDescriptorCollectionProvider,
            IEdmModel edmModel, OeDataAdapter dataAdapter)
        {
            _actionInvokerFactory = actionInvokerFactory;
            _actionSelector = actionSelector;
            _actionDescriptorCollectionProvider = actionDescriptorCollectionProvider;
            _edmModel = edmModel;
            _dataAdapter = dataAdapter;

            _operationDescriptors = GetOperationDescriptors(actionDescriptorCollectionProvider.ActionDescriptors.Items);
            _operationImports = edmModel.EntityContainer.OperationImports().ToDictionary(o => o.Name);
        }

        private static bool ActionConstaint(ActionDescriptor actionDescriptor, String httpMethod)
        {
            if (actionDescriptor.ActionConstraints == null)
                if (actionDescriptor is ControllerActionDescriptor controllerActionDescriptor)
                    return String.Compare(controllerActionDescriptor.ActionName, httpMethod, StringComparison.OrdinalIgnoreCase) == 0;
                else
                    return false;

            for (int i = 0; i < actionDescriptor.ActionConstraints.Count; i++)
                if (actionDescriptor.ActionConstraints[i] is HttpMethodActionConstraint httpMethodActionConstraint)
                    foreach (String constraintMethod in httpMethodActionConstraint.HttpMethods)
                        if (String.Compare(constraintMethod, httpMethod, StringComparison.InvariantCultureIgnoreCase) == 0)
                            return true;

            return false;
        }
        private static Dictionary<String, IReadOnlyList<ActionDescriptor>> GetOperationDescriptors(IReadOnlyList<ActionDescriptor> actionDescriptors)
        {
            return actionDescriptors.Cast<ControllerActionDescriptor>().GroupBy(o => o.ActionName).
                ToDictionary(g => g.First().ControllerName + "." + g.Key, g => (IReadOnlyList<ActionDescriptor>)g.ToList<ActionDescriptor>());
        }
        private IReadOnlyList<ActionDescriptor> GetOperationCadidates(String[] segments)
        {
            if (!_operationImports.TryGetValue(segments.Last(), out IEdmOperationImport operationImport))
                return Array.Empty<ActionDescriptor>();

            if (!_operationDescriptors.TryGetValue(operationImport.Operation.Name, out IReadOnlyList<ActionDescriptor> candidates))
                return Array.Empty<ActionDescriptor>();

            return candidates;
        }
        private static String GetPath(String path)
        {
            int pos;
            if (path[path.Length - 1] == ')')
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
                    String[] segments = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                    candidates = GetOperationCadidates(segments);

                    int length = path.Length - segments.Last().Length - 2;
                    if (length > 0)
                        candidates = candidates.Where(c => String.Compare(c.AttributeRouteInfo.Template, 0, path, 1, length, StringComparison.OrdinalIgnoreCase) == 0).ToList();
                }
                else
                {
                    String[] segments = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                    candidates = SelectCandidates(_actionDescriptorCollectionProvider.ActionDescriptors.Items, segments, context.HttpContext.Request.Method);
                    if (candidates.Count == 0)
                        candidates = GetOperationCadidates(segments);
                }
                if (candidates.Count == 0)
                    return Task.CompletedTask;
            }

            ActionDescriptor actionDescriptor = _actionSelector.SelectBestCandidate(context, candidates);
            if (actionDescriptor != null)
                context.Handler = async ctx =>
                {
                    var actionContext = new ActionContext(context.HttpContext, ctx.GetRouteData(), actionDescriptor);
                    IActionInvoker actionInvoker = _actionInvokerFactory.CreateInvoker(actionContext);
                    if (actionInvoker == null)
                        return;

                    await actionInvoker.InvokeAsync();
                };

            return Task.CompletedTask;
        }
        internal static List<ActionDescriptor> SelectCandidates(IReadOnlyList<ActionDescriptor> actionDescriptors, String[] segments, String httpMethod)
        {
            var selectCandidates = new List<ActionDescriptor>();
            for (int i = 0; i < actionDescriptors.Count; i++)
            {
                ActionDescriptor actionDescriptor = actionDescriptors[i];
                if (actionDescriptor.AttributeRouteInfo == null)
                {
                    actionDescriptor.RouteValues.TryGetValue("controller", out String value);
                    if (String.Compare(value, segments[0], StringComparison.OrdinalIgnoreCase) == 0 && ActionConstaint(actionDescriptor, httpMethod))
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
                    if (match && ActionConstaint(actionDescriptor, httpMethod))
                        selectCandidates.Add(actionDescriptor);
                }
            }

            if (selectCandidates.Count == 0 && (HttpMethods.IsPatch(httpMethod) || HttpMethods.IsDelete(httpMethod) || HttpMethods.IsPut(httpMethod)))
                return SelectCandidates(actionDescriptors, segments, HttpMethods.Post);
            return selectCandidates;
        }
    }
}
