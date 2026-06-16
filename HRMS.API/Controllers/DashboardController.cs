using HRMS.API.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HRMS.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin,HR")]
    public class DashboardController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public DashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary()
        {
            var today = DateTime.Today;

            var totalEmployees = await _context.Employees.CountAsync();
            var totalDepartments = await _context.Departments.CountAsync();
            var totalDesignations = await _context.Designations.CountAsync();

            var pendingLeaves = await _context.LeaveRequests
                .CountAsync(l => l.Status == "Pending");

            var approvedLeaves = await _context.LeaveRequests
                .CountAsync(l => l.Status == "Approved");

            var rejectedLeaves = await _context.LeaveRequests
                .CountAsync(l => l.Status == "Rejected");

            var todayAttendance = await _context.Attendance
                .CountAsync(a => a.AttendanceDate == today);

            return Ok(new
            {
                totalEmployees,
                totalDepartments,
                totalDesignations,
                pendingLeaves,
                approvedLeaves,
                rejectedLeaves,
                todayAttendance
            });
        }
    }
}