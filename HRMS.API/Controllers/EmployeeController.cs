using HRMS.API.Data;
using HRMS.API.DTOs;
using HRMS.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HRMS.API.Controllers;

[Authorize]
[Route("api/[controller]")]
[ApiController]
public class EmployeeController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    public EmployeeController(ApplicationDbContext context) => _context = context;
    private IQueryable<Employee> Employees() => _context.Employees.Include(e => e.User).Include(e => e.Department).Include(e => e.Designation);
    // Return only safe user fields; never expose password hashes.
    private static object ToResponse(Employee e) => new
    {
        e.EmployeeId, e.UserId, e.EmployeeCode, e.DepartmentId, e.DesignationId,
        e.DateOfJoining, e.Phone, e.Address, e.Salary, e.Status, e.CreatedAt,
        user = e.User == null ? null : new { e.User.UserId, e.User.FullName, e.User.Email },
        department = e.Department == null ? null : new { e.Department.DepartmentId, e.Department.DepartmentName },
        designation = e.Designation == null ? null : new { e.Designation.DesignationId, e.Designation.DesignationName }
    };

    [HttpGet]
    [Authorize(Roles = "Admin,HR")]
    public async Task<IActionResult> GetEmployees() => Ok((await Employees().AsNoTracking().OrderBy(e => e.EmployeeCode).ToListAsync()).Select(ToResponse));

    [HttpGet("users")]
    [Authorize(Roles = "Admin,HR")]
    public async Task<IActionResult> GetAvailableUsers() => Ok(await _context.Users
        .Where(u => u.IsActive && !_context.Employees.Any(e => e.UserId == u.UserId))
        .OrderBy(u => u.FullName).Select(u => new { u.UserId, u.FullName, u.Email }).ToListAsync());

    [HttpGet("{id:int}")]
    [Authorize(Roles = "Admin,HR,Employee")]
    public async Task<IActionResult> GetEmployee(int id)
    {
        var employee = await Employees().AsNoTracking().FirstOrDefaultAsync(e => e.EmployeeId == id);
        if (employee == null) return NotFound();
        if (!User.IsInRole("Admin") && !User.IsInRole("HR") && User.FindFirstValue(ClaimTypes.NameIdentifier) != employee.UserId.ToString()) return Forbid();
        return Ok(ToResponse(employee));
    }

    private async Task<IActionResult?> Validate(EmployeeCreateDto dto, Employee? existing = null)
    {
        if (string.IsNullOrWhiteSpace(dto.EmployeeCode)) return BadRequest("Enter an employee code.");
        if (dto.DateOfJoining == default) return BadRequest("Enter a joining date.");
        if (dto.Salary < 0) return BadRequest("Salary cannot be negative.");
        if (dto.Status != "Active" && dto.Status != "Inactive") return BadRequest("Select a valid status.");
        if (existing != null && existing.UserId != dto.UserId) return BadRequest("An employee's linked account cannot be changed.");
        var currentUserId = existing?.UserId ?? 0;
        var currentDepartmentId = existing?.DepartmentId ?? 0;
        var currentDesignationId = existing?.DesignationId ?? 0;
        if (!await _context.Users.AnyAsync(u => u.UserId == dto.UserId && (u.IsActive || u.UserId == currentUserId))) return BadRequest("Select an active user account.");
        if (!await _context.Departments.AnyAsync(d => d.DepartmentId == dto.DepartmentId && (d.IsActive || d.DepartmentId == currentDepartmentId))) return BadRequest("Select an active department.");
        if (!await _context.Designations.AnyAsync(d => d.DesignationId == dto.DesignationId && d.DepartmentId == dto.DepartmentId && (d.IsActive || d.DesignationId == currentDesignationId))) return BadRequest("Select an active designation belonging to the department.");
        var id = existing?.EmployeeId ?? 0;
        var code = dto.EmployeeCode.Trim().ToUpper();
        if (await _context.Employees.AnyAsync(e => e.EmployeeId != id && e.EmployeeCode.Trim().ToUpper() == code)) return Conflict("An employee with this code already exists.");
        if (await _context.Employees.AnyAsync(e => e.EmployeeId != id && e.UserId == dto.UserId)) return Conflict("This user account already has an employee record.");
        return null;
    }

    private static void Apply(Employee employee, EmployeeCreateDto dto)
    {
        employee.UserId = dto.UserId;
        employee.EmployeeCode = dto.EmployeeCode.Trim();
        employee.DepartmentId = dto.DepartmentId;
        employee.DesignationId = dto.DesignationId;
        employee.DateOfJoining = dto.DateOfJoining;
        employee.Phone = dto.Phone?.Trim();
        employee.Address = dto.Address?.Trim();
        employee.Salary = dto.Salary;
        employee.Status = dto.Status;
    }

    [HttpPost]
    [Authorize(Roles = "Admin,HR")]
    public async Task<IActionResult> CreateEmployee(EmployeeCreateDto dto)
    {
        using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var error = await Validate(dto);
        if (error != null) return error;
        var employee = new Employee { CreatedAt = DateTime.UtcNow };
        Apply(employee, dto);
        _context.Employees.Add(employee);
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return CreatedAtAction(nameof(GetEmployee), new { id = employee.EmployeeId }, ToResponse(employee));
    }

    [HttpPost("onboard")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Onboard(EmployeeOnboardDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.FullName) || string.IsNullOrWhiteSpace(dto.Email) || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(dto.Email.Trim()))
            return BadRequest("Enter a name and valid email address.");
        if (string.IsNullOrWhiteSpace(dto.Password) || dto.Password.Length < 8 || System.Text.Encoding.UTF8.GetByteCount(dto.Password) > 72)
            return BadRequest("Password must have at least 8 characters and at most 72 UTF-8 bytes.");
        using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var email = dto.Email.Trim();
        if (await _context.Users.AnyAsync(u => u.Email.Trim().ToUpper() == email.ToUpper()))
            return Conflict("Email already exists. Choose Link existing account to use that account.");
        var role = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == "Employee");
        if (role == null) return BadRequest("The Employee role is not configured.");
        var user = new User { FullName = dto.FullName.Trim(), Email = email, PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password), RoleId = role.RoleId, IsActive = true, CreatedAt = DateTime.UtcNow };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        dto.UserId = user.UserId;
        var error = await Validate(dto);
        if (error != null) return error;
        var employee = new Employee { CreatedAt = DateTime.UtcNow };
        Apply(employee, dto);
        _context.Employees.Add(employee);
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return CreatedAtAction(nameof(GetEmployee), new { id = employee.EmployeeId }, ToResponse(employee));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin,HR")]
    public async Task<IActionResult> UpdateEmployee(int id, EmployeeCreateDto dto)
    {
        using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var employee = await _context.Employees.FindAsync(id);
        if (employee == null) return NotFound("Employee no longer exists. Reload employees.");
        var error = await Validate(dto, employee);
        if (error != null) return error;
        Apply(employee, dto);
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return Ok(ToResponse(employee));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteEmployee(int id)
    {
        var employee = await _context.Employees.FindAsync(id);
        if (employee == null) return NotFound();
        _context.Employees.Remove(employee);
        await _context.SaveChangesAsync();
        return Ok();
    }
}
