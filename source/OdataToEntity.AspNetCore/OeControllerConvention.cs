using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Routing;
using System;

namespace OdataToEntity.AspNetCore
{
    public sealed class OeControllerConvention : IControllerModelConvention
    {
        public void Apply(ControllerModel controller)
        {
            for (int i = 0; i < controller.Selectors.Count; i++)
            {
                RouteAttribute routeAttribute = GetAttribute<RouteAttribute>(controller.Selectors[i]);
                String template = null;
                if (routeAttribute != null && routeAttribute.Template != null)
                {
                    if (routeAttribute.Template == "[controller]" || routeAttribute.Template == controller.ControllerName)
                    {
                        controller.Selectors[i].AttributeRouteModel = null;
                        template = controller.ControllerName;
                    }
                    else
                    {
                        int index = routeAttribute.Template.IndexOf("/[controller]");
                        if (index == -1)
                            index = routeAttribute.Template.IndexOf("/" + controller.ControllerName);
                        if (index != -1)
                        {
                            controller.Selectors[i].AttributeRouteModel.Template = routeAttribute.Template.Substring(0, index);
                            template = routeAttribute.Template.Substring(index + 1);
                        }
                    }

                    if (template != null)
                    {
                        for (int j = 0; j < controller.Actions.Count; j++)
                            Apply(controller.Actions[j], template);
                        break;
                    }
                }
            }
        }
        private static void Apply(ActionModel action, String controllerTemplate)
        {
            for (int i = 0; i < action.Selectors.Count; i++)
            {
                if (action.Selectors[i].AttributeRouteModel == null)
                {
                    HttpMethodAttribute httpMethodAttribute = GetAttribute<HttpMethodAttribute>(action.Selectors[i]);
                    if (httpMethodAttribute != null)
                        action.Selectors[i].AttributeRouteModel = new AttributeRouteModel(CreateHttpMethodAttribute(httpMethodAttribute, controllerTemplate));
                }
                else
                {
                    String template = action.Selectors[i].AttributeRouteModel.Template;
                    if (template == null)
                        action.Selectors[i].AttributeRouteModel.Template = controllerTemplate;
                    else if (template[0] == '{')
                    {
                        int index;
                        if (template[template.Length - 1] == '}')
                            action.Selectors[i].AttributeRouteModel.Template = controllerTemplate + "(" + template + ")";
                        else if ((index = template.IndexOf("}/")) > 0)
                            action.Selectors[i].AttributeRouteModel.Template = controllerTemplate + "(" + template.Substring(0, index + 1) + ")" + template.Substring(index + 1);
                    }
                    else if (template[template.Length - 1] == ')' && template.IndexOf('(') > 1)
                        action.Selectors[i].AttributeRouteModel.Template = controllerTemplate + "/" + template;
                }
            }
        }
        private static HttpMethodAttribute CreateHttpMethodAttribute(HttpMethodAttribute httpMethodAttribute, String controllerTemplate)
        {
            if (httpMethodAttribute is HttpDeleteAttribute)
                return new HttpDeleteAttribute(controllerTemplate);
            if (httpMethodAttribute is HttpGetAttribute)
                return new HttpGetAttribute(controllerTemplate);
            if (httpMethodAttribute is HttpHeadAttribute)
                return new HttpHeadAttribute(controllerTemplate);
            if (httpMethodAttribute is HttpOptionsAttribute)
                return new HttpOptionsAttribute(controllerTemplate);
            if (httpMethodAttribute is HttpPatchAttribute)
                return new HttpPatchAttribute(controllerTemplate);
            if (httpMethodAttribute is HttpPostAttribute)
                return new HttpPostAttribute(controllerTemplate);
            if (httpMethodAttribute is HttpPutAttribute)
                return new HttpPutAttribute(controllerTemplate);

            throw new InvalidOperationException("Unknown HttpMethodAttribute " + httpMethodAttribute.GetType().FullName);
        }
        private static T GetAttribute<T>(SelectorModel selectorModel) where T : Attribute, IRouteTemplateProvider
        {
            for (int i = 0; i < selectorModel.EndpointMetadata.Count; i++)
                if (selectorModel.EndpointMetadata[i] is T attribute)
                    return attribute;

            return null;
        }
    }
}
