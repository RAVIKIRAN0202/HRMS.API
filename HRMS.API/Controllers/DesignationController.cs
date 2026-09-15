using HRMS.API.Data;
using HRMS.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HRMS.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin,HR")]
    public class DesignationController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public DesignationController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetDesignations()
        {
            var designations = await _context.Designations
                .Include(d => d.Department)
                .ToListAsync();

            return Ok(designations);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetDesignation(int id)
        {
            var designation = await _context.Designations
                .Include(d => d.Department)
                .FirstOrDefaultAsync(d => d.DesignationId == id);

            if (designation == null)
                return NotFound();

            return Ok(designation);
        }

        [HttpPost]
        public async Task<IActionResult> CreateDesignation(Designation designation)
        {
            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var error = await Validate(designation);
            if (error != null) return error;
            designation.DesignationId = 0;
            designation.DesignationName = designation.DesignationName.Trim();
            designation.Department = null;
            _context.Designations.Add(designation);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(designation);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateDesignation(int id, Designation designation)
        {
            if (id != designation.DesignationId)
                return BadRequest();

            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var existing = await _context.Designations.FindAsync(id);
            if (existing == null) return NotFound();
            var error = await Validate(designation, existing);
            if (error != null) return error;
            if (existing.DepartmentId != designation.DepartmentId && await _context.Employees.AnyAsync(e => e.DesignationId == id))
                return BadRequest("This designation is assigned to employees. Create a new designation in the other department.");
            existing.DesignationName = designation.DesignationName.Trim();
            existing.DepartmentId = designation.DepartmentId;
            existing.IsActive = designation.IsActive;
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(designation);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteDesignation(int id)
        {
            var designation = await _context.Designations.FindAsync(id);

            if (designation == null)
                return NotFound();

            _context.Designations.Remove(designation);
            await _context.SaveChangesAsync();

            return Ok();
        }

        private async Task<IActionResult?> Validate(Designation value, Designation? existing = null)
        {
            if (string.IsNullOrWhiteSpace(value.DesignationName)) return BadRequest("Enter a designation name.");
            var previousDepartment = existing?.DepartmentId ?? 0;
            if (!await _context.Departments.AnyAsync(d => d.DepartmentId == value.DepartmentId && (d.IsActive || d.DepartmentId == previousDepartment)))
                return BadRequest("Select an active department.");
            var id = existing?.DesignationId ?? 0;
            var name = value.DesignationName.Trim().ToUpper();
            if (await _context.Designations.AnyAsync(d => d.DesignationId != id && d.DepartmentId == value.DepartmentId && d.DesignationName.Trim().ToUpper() == name))
                return Conflict("This designation already exists in the department.");
            return null;
        }
    }
}
