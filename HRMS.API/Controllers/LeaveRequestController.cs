using HRMS.API.DTOs;
using HRMS.API.Models;
using HRMS.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = "Admin,HR,Employee")]
public class LeaveRequestController(ILeaveRequestService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAllLeaveRequests() =>
        this.ToActionResult(await service.GetAllLeaveRequests());

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetLeaveRequest(int id) =>
        this.ToActionResult(await service.GetLeaveRequest(id));

    [HttpPost]
    public async Task<IActionResult> ApplyLeave(LeaveRequestCreateDto dto) =>
        this.ToActionResult(await service.ApplyLeave(dto));

    [HttpPut("approve/{id:int}")]
    [Authorize(Roles = "Admin,HR")]
    public async Task<IActionResult> ApproveLeave(int id, LeaveReviewDto dto) =>
        this.ToActionResult(await service.ApproveLeave(id, dto));

    [HttpPut("reject/{id:int}")]
    [Authorize(Roles = "Admin,HR")]
    public async Task<IActionResult> RejectLeave(int id, LeaveReviewDto dto) =>
        this.ToActionResult(await service.RejectLeave(id, dto));

    [HttpGet("balances")]
    public async Task<IActionResult> GetBalances([FromQuery] int? year) =>
        this.ToActionResult(await service.GetBalances(year));
}
