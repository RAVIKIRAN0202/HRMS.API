using HRMS.API.Data;
using HRMS.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HRMS.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin,HR")]
    public class DepartmentController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public DepartmentController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetDepartments()
        {
            return Ok(await _context.Departments.ToListAsync());
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetDepartment(int id)
        {
            var department = await _context.Departments.FindAsync(id);

            if (department == null)
                return NotFound();

            return Ok(department);
        }

        [HttpPost]
        public async Task<IActionResult> CreateDepartment(Department department)
        {
            if (string.IsNullOrWhiteSpace(department.DepartmentName))
                return BadRequest("Enter a department name.");
            department.DepartmentName = department.DepartmentName.Trim();
            department.Description = department.Description?.Trim();
            department.DepartmentId = 0;
            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            if (await _context.Departments.AnyAsync(d => d.DepartmentName.Trim().ToUpper() == department.DepartmentName.ToUpper()))
                return Conflict("A department with this name already exists.");
            _context.Departments.Add(department);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(department);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateDepartment(int id, Department department)
        {
            if (id != department.DepartmentId)
                return BadRequest();

            if (string.IsNullOrWhiteSpace(department.DepartmentName))
                return BadRequest("Enter a department name.");
            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var existing = await _context.Departments.FindAsync(id);
            if (existing == null)
                return NotFound("Department no longer exists. Reload departments.");
            var name = department.DepartmentName.Trim();
            if (await _context.Departments.AnyAsync(d => d.DepartmentId != id && d.DepartmentName.Trim().ToUpper() == name.ToUpper()))
                return Conflict("A department with this name already exists.");
            existing.DepartmentName = name;
            existing.Description = department.Description?.Trim();
            existing.IsActive = department.IsActive;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(existing);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteDepartment(int id)
        {
            var department = await _context.Departments.FindAsync(id);

            if (department == null)
                return NotFound();

            _context.Departments.Remove(department);

            await _context.SaveChangesAsync();

            return Ok();
        }
    }
}
