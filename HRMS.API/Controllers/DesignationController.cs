using HRMS.API.DTOs;
using HRMS.API.Models;
using HRMS.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = "Admin,HR")]
public class DesignationController(IDesignationService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetDesignations() =>
        this.ToActionResult(await service.GetDesignations());

    [HttpGet("{id}")]
    public async Task<IActionResult> GetDesignation(int id) =>
        this.ToActionResult(await service.GetDesignation(id));

    [HttpPost]
    public async Task<IActionResult> CreateDesignation(Designation designation) =>
        this.ToActionResult(await service.CreateDesignation(designation));

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateDesignation(int id, Designation designation) =>
        this.ToActionResult(await service.UpdateDesignation(id, designation));

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteDesignation(int id) =>
        this.ToActionResult(await service.DeleteDesignation(id));
}
