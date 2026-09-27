using HRMS.API.DTOs;
using HRMS.API.Models;
using HRMS.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class AttendanceController(IAttendanceService service) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = "Admin,HR,Employee")]
    public async Task<IActionResult> GetAttendance() =>
        this.ToActionResult(await service.GetAttendance());

    [HttpGet("{id}")]
    [Authorize(Roles = "Admin,HR,Employee")]
    public async Task<IActionResult> GetAttendanceById(int id) =>
        this.ToActionResult(await service.GetAttendanceById(id));

    [HttpPost("check-in")]
    [Authorize(Roles = "Admin,HR,Employee")]
    public async Task<IActionResult> CheckIn() =>
        this.ToActionResult(await service.CheckIn());

    [HttpPut("check-out/{attendanceId}")]
    [Authorize(Roles = "Admin,HR,Employee")]
    public async Task<IActionResult> CheckOut(int attendanceId) =>
        this.ToActionResult(await service.CheckOut(attendanceId));

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteAttendance(int id) =>
        this.ToActionResult(await service.DeleteAttendance(id));
}
