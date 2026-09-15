using HRMS.API.Data;
using HRMS.API.DTOs;
using HRMS.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HRMS.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class AttendanceController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private int UserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

        public AttendanceController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        [Authorize(Roles = "Admin,HR,Employee")]
        public async Task<IActionResult> GetAttendance()
        {
            var attendance = await _context.Attendance
                .Where(a => User.IsInRole("Admin") || User.IsInRole("HR") || (a.Employee != null && a.Employee.UserId == UserId))
                .OrderByDescending(a => a.AttendanceDate)
                .Select(a => new { a.AttendanceId, a.EmployeeId, a.AttendanceDate, a.CheckInTime, a.CheckOutTime, a.Status,
                    employeeName = a.Employee == null || a.Employee.User == null ? null : a.Employee.User.FullName,
                    employeeCode = a.Employee == null ? null : a.Employee.EmployeeCode })
                .ToListAsync();

            return Ok(attendance);
        }

        [HttpGet("{id}")]
        [Authorize(Roles = "Admin,HR,Employee")]
        public async Task<IActionResult> GetAttendanceById(int id)
        {
            var attendance = await _context.Attendance
                .Include(a => a.Employee)
                .FirstOrDefaultAsync(a => a.AttendanceId == id);

            if (attendance == null)
                return NotFound();

            if (!User.IsInRole("Admin") && !User.IsInRole("HR") && attendance.Employee?.UserId != UserId) return Forbid();

            return Ok(attendance);
        }

        [HttpPost("check-in")]
        [Authorize(Roles = "Admin,HR,Employee")]
        public async Task<IActionResult> CheckIn()
        {
            var today = DateTime.Today;
            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var employee = await _context.Employees.FirstOrDefaultAsync(e => e.UserId == UserId && e.Status == "Active");
            if (employee == null) return BadRequest("An active employee profile is required. Contact HR.");
            if (employee.DateOfJoining.Date > today) return BadRequest("You cannot check in before your joining date.");

            var alreadyCheckedIn = await _context.Attendance
                .AnyAsync(a => a.EmployeeId == employee.EmployeeId &&
                               a.AttendanceDate == today);

            if (alreadyCheckedIn)
                return BadRequest("Employee already checked in today");

            var attendance = new Attendance
            {
                EmployeeId = employee.EmployeeId,
                AttendanceDate = today,
                CheckInTime = DateTime.Now,
                Status = "Present"
            };

            _context.Attendance.Add(attendance);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(attendance);
        }

        [HttpPut("check-out/{attendanceId}")]
        [Authorize(Roles = "Admin,HR,Employee")]
        public async Task<IActionResult> CheckOut(int attendanceId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var attendance = await _context.Attendance.Include(a => a.Employee).FirstOrDefaultAsync(a => a.AttendanceId == attendanceId && a.Employee != null && a.Employee.UserId == UserId);

            

            if (attendance == null)
                return NotFound();

            if (attendance.CheckOutTime != null)
                return BadRequest("Employee already checked out");

            if (attendance.CheckInTime == null || attendance.CheckInTime > DateTime.Now) return BadRequest("A valid check-in is required before checkout.");

            attendance.CheckOutTime = DateTime.Now;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(attendance);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteAttendance(int id)
        {
            var attendance = await _context.Attendance.FindAsync(id);

            if (attendance == null)
                return NotFound();

            _context.Attendance.Remove(attendance);
            await _context.SaveChangesAsync();

            return Ok();
        }
    }
}
