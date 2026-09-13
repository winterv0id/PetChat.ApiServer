using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using PetChat.ApiServer.Model.Utils;

namespace PetChat.ApiServer.Tests.Unit;

/// <summary>
/// <see cref="DotNotationRoutingConvention"/> заменяет обычные маршруты
/// на шаблон "{controller}.{action}" (в нижнем регистре) для всех контроллеров.
public class DotNotationRoutingConventionTests
{
    private class SampleController : ControllerBase
    {
        // по умолчанию должен стать [HttpGet]
        public IActionResult GetProfile() => Ok();

        [HttpPost]
        public IActionResult Auth() => Ok();
    }

    [Fact]
    public void Apply_ActionWithoutHttpAttribute_GetsDotNotationRoute_AndDefaultsToGet()
    {
        var controllerModel = BuildControllerModel("Sample", nameof(SampleController.GetProfile), out var action);

        new DotNotationRoutingConvention().Apply(controllerModel);

        var selector = Assert.Single(action.Selectors);
        Assert.Equal("sample.getProfile", selector.AttributeRouteModel!.Template);

        var constraint = Assert.IsType<HttpMethodActionConstraint>(Assert.Single(selector.ActionConstraints));
        Assert.Equal(["GET"], constraint.HttpMethods);
    }

    [Fact]
    public void Apply_ActionNameFirstLetter_IsLowercased()
    {
        // camelCase, lower только первый символ в названии метода  
        var controllerModel = BuildControllerModel("Sample", nameof(SampleController.GetProfile), out var action);

        new DotNotationRoutingConvention().Apply(controllerModel);

        Assert.Equal("sample.getProfile", action.Selectors[0].AttributeRouteModel!.Template);
    }

    [Fact]
    public void Apply_ControllerName_IsLowercasedInTemplate()
    {
        var controllerModel = BuildControllerModel("Sample", nameof(SampleController.GetProfile), out _);
        // конвенция обязана нормализовать название контроллера через ToLowerInvariant()
        controllerModel.ControllerName = "SaMpLe";

        new DotNotationRoutingConvention().Apply(controllerModel);

        Assert.StartsWith("sample.", controllerModel.Actions[0].Selectors[0].AttributeRouteModel!.Template);
    }

    [Fact]
    public void Apply_ActionWithHttpPostAttribute_UsesPostConstraint()
    {
        var controllerModel = BuildControllerModel("Sample", nameof(SampleController.Auth), out var action);

        new DotNotationRoutingConvention().Apply(controllerModel);

        var selector = Assert.Single(action.Selectors);
        Assert.Equal("sample.auth", selector.AttributeRouteModel!.Template);

        var constraint = Assert.IsType<HttpMethodActionConstraint>(Assert.Single(selector.ActionConstraints));
        Assert.Equal(["POST"], constraint.HttpMethods);
    }

    [Fact]
    public void Apply_PreExistingSelectors_AreClearedAndReplaced()
    {
        // проверяет вызов Selectors.Clear() конвенцией перед добавлением своих правил
        var controllerModel = BuildControllerModel("Sample", nameof(SampleController.GetProfile), out var action);
        action.Selectors.Add(new SelectorModel
        {
            AttributeRouteModel = new AttributeRouteModel(new RouteAttribute("some/sample/route"))
        });

        new DotNotationRoutingConvention().Apply(controllerModel);

        Assert.Single(action.Selectors);
        Assert.Equal("sample.getProfile", action.Selectors[0].AttributeRouteModel!.Template);
    }

    private static ControllerModel BuildControllerModel(string controllerName, string actionMethodName, out ActionModel action)
    {
        var controllerType = typeof(SampleController).GetTypeInfo();
        var controllerModel = new ControllerModel(controllerType, [])
        {
            ControllerName = controllerName
        };

        var methodInfo = typeof(SampleController).GetMethod(actionMethodName)!;
        var attributes = methodInfo.GetCustomAttributes(inherit: true).ToList();

        action = new ActionModel(methodInfo, attributes)
        {
            ActionName = actionMethodName,
            Controller = controllerModel
        };
        controllerModel.Actions.Add(action);

        return controllerModel;
    }
}