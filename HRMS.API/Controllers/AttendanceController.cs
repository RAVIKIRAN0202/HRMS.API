using HRMS.API.Data;
using HRMS.API.DTOs;
using HRMS.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HRMS.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class AttendanceController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public AttendanceController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        [Authorize(Roles = "Admin,HR")]
        public async Task<IActionResult> GetAttendance()
        {
            var attendance = await _context.Attendance
                .Include(a => a.Employee)
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

            return Ok(attendance);
        }

        [HttpPost("check-in")]
        [Authorize(Roles = "Admin,HR,Employee")]
        public async Task<IActionResult> CheckIn(AttendanceCheckInDto dto)
        {
            var today = DateTime.Today;

            var alreadyCheckedIn = await _context.Attendance
                .AnyAsync(a => a.EmployeeId == dto.EmployeeId &&
                               a.AttendanceDate == today);

            if (alreadyCheckedIn)
                return BadRequest("Employee already checked in today");

            var attendance = new Attendance
            {
                EmployeeId = dto.EmployeeId,
                AttendanceDate = today,
                CheckInTime = DateTime.Now,
                Status = "Present"
            };

            _context.Attendance.Add(attendance);
            await _context.SaveChangesAsync();

            return Ok(attendance);
        }

        [HttpPut("check-out/{attendanceId}")]
        [Authorize(Roles = "Admin,HR,Employee")]
        public async Task<IActionResult> CheckOut(int attendanceId)
        {
            var attendance = await _context.Attendance.FindAsync(attendanceId);

            if (attendance == null)
                return NotFound();

            if (attendance.CheckOutTime != null)
                return BadRequest("Employee already checked out");

            attendance.CheckOutTime = DateTime.Now;

            await _context.SaveChangesAsync();

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