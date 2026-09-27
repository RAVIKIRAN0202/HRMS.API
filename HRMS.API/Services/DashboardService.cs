using HRMS.API.Data;
using Microsoft.EntityFrameworkCore;

namespace HRMS.API.Services
{

    public sealed class DashboardService : IDashboardService
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentUser _currentUser;

        public DashboardService(ApplicationDbContext context, ICurrentUser currentUser)
        {
            _context = context;
            _currentUser = currentUser;
        }

        public async Task<ServiceResult> GetSummary()
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

            return ServiceResult.Success(new
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

        public async Task<ServiceResult> GetMyDashboard()
        {
            if (!int.TryParse(_currentUser.Subject, out var userId))
                return ServiceResult.Unauthenticated();
            var employee = await _context.Employees.AsNoTracking()
                .Where(e => e.UserId == userId)
                .Select(e => new
                {
                    e.EmployeeId,
                    e.EmployeeCode,
                    e.DateOfJoining,
                    e.Phone,
                    e.Address,
                    e.Status,
                    fullName = e.User == null ? null : e.User.FullName,
                    email = e.User == null ? null : e.User.Email,
                    department = e.Department == null ? null : e.Department.DepartmentName,
                    designation = e.Designation == null ? null : e.Designation.DesignationName
                }).FirstOrDefaultAsync();
            if (employee == null)
                return ServiceResult.Missing("Your account is not linked to an employee record yet. Please contact HR.");
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
            return ServiceResult.Success(new { employee, pendingLeaves, approvedLeaves, rejectedLeaves, attendance });
        }
    }
}
