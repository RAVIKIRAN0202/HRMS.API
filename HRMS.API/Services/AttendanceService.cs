using HRMS.API.Data;
using HRMS.API.DTOs;
using HRMS.API.Models;
using Microsoft.EntityFrameworkCore;

namespace HRMS.API.Services
{

    public sealed class AttendanceService : IAttendanceService
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentUser _currentUser;
        private int UserId => int.TryParse(_currentUser.Subject, out var id) ? id : 0;

        public AttendanceService(ApplicationDbContext context, ICurrentUser currentUser)
        {
            _context = context;
            _currentUser = currentUser;
        }

        public async Task<ServiceResult> GetAttendance()
        {
            var attendance = await _context.Attendance
                .Where(a => _currentUser.IsInRole("Admin") || _currentUser.IsInRole("HR") || (a.Employee != null && a.Employee.UserId == UserId))
                .OrderByDescending(a => a.AttendanceDate)
                .Select(a => new
                {
                    a.AttendanceId,
                    a.EmployeeId,
                    a.AttendanceDate,
                    a.CheckInTime,
                    a.CheckOutTime,
                    a.Status,
                    employeeName = a.Employee == null || a.Employee.User == null ? null : a.Employee.User.FullName,
                    employeeCode = a.Employee == null ? null : a.Employee.EmployeeCode
                })
                .ToListAsync();

            return ServiceResult.Success(attendance);
        }

        public async Task<ServiceResult> GetAttendanceById(int id)
        {
            var attendance = await _context.Attendance
                .Include(a => a.Employee)
                .FirstOrDefaultAsync(a => a.AttendanceId == id);

            if (attendance == null)
                return ServiceResult.Missing();

            if (!_currentUser.IsInRole("Admin") && !_currentUser.IsInRole("HR") && attendance.Employee?.UserId != UserId) return ServiceResult.Forbidden();

            return ServiceResult.Success(attendance);
        }

        public async Task<ServiceResult> CheckIn()
        {
            var today = DateTime.Today;
            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var employee = await _context.Employees.FirstOrDefaultAsync(e => e.UserId == UserId && e.Status == "Active");
            if (employee == null) return ServiceResult.Invalid("An active employee profile is required. Contact HR.");
            if (employee.DateOfJoining.Date > today) return ServiceResult.Invalid("You cannot check in before your joining date.");

            var alreadyCheckedIn = await _context.Attendance
                .AnyAsync(a => a.EmployeeId == employee.EmployeeId &&
                               a.AttendanceDate == today);

            if (alreadyCheckedIn)
                return ServiceResult.Invalid("Employee already checked in today");

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

            return ServiceResult.Success(attendance);
        }

        public async Task<ServiceResult> CheckOut(int attendanceId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var attendance = await _context.Attendance.Include(a => a.Employee).FirstOrDefaultAsync(a => a.AttendanceId == attendanceId && a.Employee != null && a.Employee.UserId == UserId);

            if (attendance == null)
                return ServiceResult.Missing();

            if (attendance.CheckOutTime != null)
                return ServiceResult.Invalid("Employee already checked out");

            if (attendance.CheckInTime == null || attendance.CheckInTime > DateTime.Now) return ServiceResult.Invalid("A valid check-in is required before checkout.");

            attendance.CheckOutTime = DateTime.Now;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return ServiceResult.Success(attendance);
        }

        public async Task<ServiceResult> DeleteAttendance(int id)
        {
            var attendance = await _context.Attendance.FindAsync(id);

            if (attendance == null)
                return ServiceResult.Missing();

            _context.Attendance.Remove(attendance);
            await _context.SaveChangesAsync();

            return ServiceResult.Success();
        }
    }
}
