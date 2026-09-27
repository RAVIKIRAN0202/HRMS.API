using HRMS.API.DTOs;
using HRMS.API.Models;
using HRMS.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS.API.Controllers;

[Authorize]
[Route("api/[controller]")]
[ApiController]
public class EmployeeController(IEmployeeService service) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = "Admin,HR")]
    public async Task<IActionResult> GetEmployees() =>
        this.ToActionResult(await service.GetEmployees());

    [HttpGet("users")]
    [Authorize(Roles = "Admin,HR")]
    public async Task<IActionResult> GetAvailableUsers() =>
        this.ToActionResult(await service.GetAvailableUsers());

    [HttpGet("{id:int}")]
    [Authorize(Roles = "Admin,HR,Employee")]
    public async Task<IActionResult> GetEmployee(int id) =>
        this.ToActionResult(await service.GetEmployee(id));

    [HttpPost]
    [Authorize(Roles = "Admin,HR")]
    public async Task<IActionResult> CreateEmployee(EmployeeCreateDto dto) =>
        this.ToActionResult(await service.CreateEmployee(dto), nameof(GetEmployee));

    [HttpPost("onboard")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Onboard(EmployeeOnboardDto dto) =>
        this.ToActionResult(await service.Onboard(dto), nameof(GetEmployee));

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin,HR")]
    public async Task<IActionResult> UpdateEmployee(int id, EmployeeCreateDto dto) =>
        this.ToActionResult(await service.UpdateEmployee(id, dto));

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteEmployee(int id) =>
        this.ToActionResult(await service.DeleteEmployee(id));
}
