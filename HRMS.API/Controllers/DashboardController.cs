using HRMS.API.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HRMS.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class DashboardController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public DashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("summary")]
        [Authorize(Roles = "Admin,HR")]
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

        [HttpGet("me")]
        [Authorize(Roles = "Employee")]
        public async Task<IActionResult> GetMyDashboard()
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return Unauthorized();
            var employee = await _context.Employees.AsNoTracking()
                .Where(e => e.UserId == userId)
                .Select(e => new {
                    e.EmployeeId, e.EmployeeCode, e.DateOfJoining, e.Phone, e.Address, e.Status,
                    fullName = e.User == null ? null : e.User.FullName,
                    email = e.User == null ? null : e.User.Email,
                    department = e.Department == null ? null : e.Department.DepartmentName,
                    designation = e.Designation == null ? null : e.Designation.DesignationName
                }).FirstOrDefaultAsync();
            if (employee == null)
                return NotFound("Your account is not linked to an employee record yet. Please contact HR.");
            var leaves = _context.LeaveRequests.Where(l => l.EmployeeId == employee.EmployeeId);
            var pendingLeaves = await leaves.CountAsync(l => l.Status == "Pending");
            var approvedLeaves = await leaves.CountAsync(l => l.Status == "Approved");
            var rejectedLeaves = await leaves.CountAsync(l => l.Status == "Rejected");
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);
            var attendance = await _context.Attendance.AsNoTracking()
                .Where(a => a.EmployeeId == employee.EmployeeId && a.AttendanceDate >= today && a.AttendanceDate < tomorrow)
                .OrderByDescending(a => a.AttendanceId)
                .Select(a => new { a.Status, a.CheckInTime, a.CheckOutTime }).FirstOrDefaultAsync();
            return Ok(new { employee, pendingLeaves, approvedLeaves, rejectedLeaves, attendance });
        }
    }
}
