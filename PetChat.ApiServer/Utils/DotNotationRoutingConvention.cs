using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Routing;

namespace PetChat.ApiServer.Model.Utils;

public class DotNotationRoutingConvention : IControllerModelConvention
{
    public void Apply(ControllerModel controller)
    {
        var category = controller.ControllerName.ToLowerInvariant();

        foreach (var action in controller.Actions)
        {
            var actionName = char.ToLower(action.ActionName[0]) + action.ActionName[1..];
            var template = $"{category}.{actionName}";

            action.Selectors.Clear();
            action.Selectors.Add(new SelectorModel
            {
                AttributeRouteModel = new AttributeRouteModel(new RouteAttribute(template)),
                ActionConstraints = { GetHttpMethodConstraint(action) }
            });
        }
    }

    private IActionConstraintMetadata GetHttpMethodConstraint(ActionModel action)
    {
        // всё GET, кроме помеченных [HttpPost])
        var httpMethod = action.Attributes.OfType<HttpMethodAttribute>().FirstOrDefault()?.HttpMethods.First() ?? "GET";
        return new HttpMethodActionConstraint([httpMethod]);
    }
}