using HRMS.API.DTOs;
using HRMS.API.Models;
using HRMS.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class DashboardController(IDashboardService service) : ControllerBase
{
    [HttpGet("summary")]
    [Authorize(Roles = "Admin,HR")]
    public async Task<IActionResult> GetSummary() =>
        this.ToActionResult(await service.GetSummary());

    [HttpGet("me")]
    [Authorize(Roles = "Employee")]
    public async Task<IActionResult> GetMyDashboard() =>
        this.ToActionResult(await service.GetMyDashboard());
}
