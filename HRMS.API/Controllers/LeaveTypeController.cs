using HRMS.API.DTOs;
using HRMS.API.Models;
using HRMS.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class LeaveTypeController(ILeaveTypeService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetLeaveTypes() =>
        this.ToActionResult(await service.GetLeaveTypes());

    [HttpPost]
    [Authorize(Roles = "Admin,HR")]
    public async Task<IActionResult> CreateLeaveType(LeaveType leaveType) =>
        this.ToActionResult(await service.CreateLeaveType(leaveType));

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin,HR")]
    public async Task<IActionResult> UpdateLeaveType(int id, LeaveType value) =>
        this.ToActionResult(await service.UpdateLeaveType(id, value));
}
