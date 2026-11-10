using Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

/// <summary>Maps use-case errors to RFC 9457 problem details.</summary>
internal static class ServiceErrorMapping
{
    public const string ConflictType = "https://docboo.local/problems/version-conflict";
    public const string NotFoundType = "https://docboo.local/problems/not-found";

    public static ActionResult ToActionResult(this ControllerBase controller, ServiceError error) => error switch
    {
        ServiceError.NotFound => controller.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Recurso não encontrado.",
            type: NotFoundType),

        ServiceError.Invalid invalid => controller.ValidationProblem(ToModelState(invalid.Errors)),

        ServiceError.Conflict conflict => ConflictProblem(controller, conflict.CurrentVersion),

        _ => throw new NotSupportedException(error.GetType().Name),
    };

    private static ObjectResult ConflictProblem(ControllerBase controller, int currentVersion)
    {
        var problem = controller.ProblemDetailsFactory.CreateProblemDetails(
            controller.HttpContext,
            statusCode: StatusCodes.Status409Conflict,
            title: "O trabalho foi alterado por outra sessão.",
            type: ConflictType,
            detail: "Recarregue o trabalho e aplique suas alterações novamente.");
        problem.Extensions["currentVersion"] = currentVersion;
        return new ObjectResult(problem) { StatusCode = StatusCodes.Status409Conflict };
    }

    private static Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary ToModelState(IReadOnlyList<ValidationError> errors)
    {
        var state = new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary();
        foreach (var e in errors) state.AddModelError(e.Path, e.Message);
        return state;
    }
}
