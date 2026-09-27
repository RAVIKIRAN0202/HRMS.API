using HRMS.API.DTOs;
using HRMS.API.Models;
using HRMS.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = "Admin,HR")]
public class DepartmentController(IDepartmentService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetDepartments() =>
        this.ToActionResult(await service.GetDepartments());

    [HttpGet("{id}")]
    public async Task<IActionResult> GetDepartment(int id) =>
        this.ToActionResult(await service.GetDepartment(id));

    [HttpPost]
    public async Task<IActionResult> CreateDepartment(Department department) =>
        this.ToActionResult(await service.CreateDepartment(department));

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateDepartment(int id, Department department) =>
        this.ToActionResult(await service.UpdateDepartment(id, department));

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteDepartment(int id) =>
        this.ToActionResult(await service.DeleteDepartment(id));
}
