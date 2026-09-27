using HRMS.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace HRMS.API.Controllers;

public static class ServiceResultExtensions
{
    public static IActionResult ToActionResult(this ControllerBase controller, ServiceResult result, string? getAction = null) => result.Outcome switch
    {
        ServiceOutcome.Success => result.Value is null ? controller.Ok() : controller.Ok(result.Value),
        ServiceOutcome.Created => controller.CreatedAtAction(getAction ?? throw new InvalidOperationException("A resource lookup action is required."), new { id = result.ResourceId }, result.Value),
        ServiceOutcome.Invalid => result.Value is null ? controller.BadRequest() : controller.BadRequest(result.Value),
        ServiceOutcome.Missing => result.Value is null ? controller.NotFound() : controller.NotFound(result.Value),
        ServiceOutcome.Conflict => result.Value is null ? controller.Conflict() : controller.Conflict(result.Value),
        ServiceOutcome.Unauthenticated => result.Value is null ? controller.Unauthorized() : controller.Unauthorized(result.Value),
        ServiceOutcome.Forbidden => controller.Forbid(),
        _ => throw new ArgumentOutOfRangeException(nameof(result))
    };
}
