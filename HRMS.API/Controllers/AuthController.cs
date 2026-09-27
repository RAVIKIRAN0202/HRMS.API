using HRMS.API.DTOs;
using HRMS.API.Models;
using HRMS.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController(IAuthService service) : ControllerBase
{
    [HttpPost("register")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Register(RegisterDto dto) =>
        this.ToActionResult(await service.Register(dto));

    [HttpPost("register-employee")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RegisterEmployee(RegisterDto dto) =>
        this.ToActionResult(await service.RegisterEmployee(dto));

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(ChangePasswordDto dto) =>
        this.ToActionResult(await service.ChangePassword(dto));

    [HttpPost("reset-password/{employeeId:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ResetPassword(int employeeId, ResetPasswordDto dto) =>
        this.ToActionResult(await service.ResetPassword(employeeId, dto));

    [HttpPost("login")]
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public async Task<IActionResult> Login(LoginDto dto) =>
        this.ToActionResult(await service.Login(dto));

    [HttpGet("session")]
    [Authorize]
    public async Task<IActionResult> GetSession() =>
        this.ToActionResult(await service.GetSession());
}
