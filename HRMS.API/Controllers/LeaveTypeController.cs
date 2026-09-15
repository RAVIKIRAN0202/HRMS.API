using HRMS.API.Data;
using HRMS.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HRMS.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class LeaveTypeController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public LeaveTypeController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetLeaveTypes()
        {
            return Ok(await _context.LeaveTypes.ToListAsync());
        }

        [HttpPost]
        [Authorize(Roles = "Admin,HR")]
        public async Task<IActionResult> CreateLeaveType(LeaveType leaveType)
        {
            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var error = await Validate(leaveType, 0);
            if (error != null) return error;
            leaveType.LeaveTypeId = 0;
            leaveType.LeaveTypeName = leaveType.LeaveTypeName.Trim();
            leaveType.CreatedAt = DateTime.Now;

            _context.LeaveTypes.Add(leaveType);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(leaveType);
        }

        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin,HR")]
        public async Task<IActionResult> UpdateLeaveType(int id, LeaveType value)
        {
            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var existing = await _context.LeaveTypes.FindAsync(id);
            if (existing == null) return NotFound();
            var error = await Validate(value, id);
            if (error != null) return error;
            existing.LeaveTypeName = value.LeaveTypeName.Trim();
            existing.MaxDays = value.MaxDays;
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return Ok(existing);
        }

        private async Task<IActionResult?> Validate(LeaveType value, int id)
        {
            if (string.IsNullOrWhiteSpace(value.LeaveTypeName)) return BadRequest("Enter a leave type name.");
            if (value.MaxDays < 1) return BadRequest("Maximum days must be a positive whole number.");
            var name = value.LeaveTypeName.Trim().ToUpper();
            if (await _context.LeaveTypes.AnyAsync(t => t.LeaveTypeId != id && t.LeaveTypeName.Trim().ToUpper() == name))
                return Conflict("This leave type already exists.");
            return null;
        }
    }
}
