using HRMS.API.Data;
using HRMS.API.DTOs;
using Microsoft.EntityFrameworkCore;

namespace HRMS.API.Services;

public sealed class LeaveRequestService : ILeaveRequestService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUser _currentUser;
    public LeaveRequestService(ApplicationDbContext context, ICurrentUser currentUser) { _context = context; _currentUser = currentUser; }
    private int UserId => int.TryParse(_currentUser.Subject, out var id) ? id : 0;
    private bool Manager => _currentUser.IsInRole("Admin") || _currentUser.IsInRole("HR");
    private IQueryable<Models.LeaveRequest> Visible() => _context.LeaveRequests.AsNoTracking()
        .Where(l => Manager || (l.Employee != null && l.Employee.UserId == UserId));
    private static IQueryable<object> Project(IQueryable<Models.LeaveRequest> query) => query.Select(l => new
    {
        l.LeaveRequestId,
        l.EmployeeId,
        l.LeaveTypeId,
        l.FromDate,
        l.ToDate,
        l.Reason,
        l.Status,
        l.ReviewComment,
        l.ApprovedBy,
        employeeName = l.Employee == null || l.Employee.User == null ? null : l.Employee.User.FullName,
        employeeCode = l.Employee == null ? null : l.Employee.EmployeeCode,
        leaveTypeName = l.LeaveType == null ? null : l.LeaveType.LeaveTypeName
    });

    public async Task<ServiceResult> GetAllLeaveRequests() => ServiceResult.Success(await Project(Visible().OrderByDescending(l => l.CreatedAt)).ToListAsync());

    public async Task<ServiceResult> GetLeaveRequest(int id)
    {
        var leave = await Project(Visible().Where(l => l.LeaveRequestId == id)).FirstOrDefaultAsync();
        return leave == null ? ServiceResult.Missing() : ServiceResult.Success(leave);
    }

    public async Task<ServiceResult> ApplyLeave(LeaveRequestCreateDto dto)
    {
        if (dto.FromDate == default || dto.ToDate == default || dto.ToDate.Year > 9998 || dto.ToDate.Date < dto.FromDate.Date)
            return ServiceResult.Invalid("Enter valid dates with the end date on or after the start date.");
        using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var employee = await _context.Employees.FirstOrDefaultAsync(e => e.UserId == UserId);
        if (employee == null || employee.Status != "Active") return ServiceResult.Invalid("An active employee profile is required. Please contact HR.");
        if (dto.FromDate.Date < employee.DateOfJoining.Date) return ServiceResult.Invalid("Leave cannot start before your joining date.");
        if (!await _context.LeaveTypes.AnyAsync(t => t.LeaveTypeId == dto.LeaveTypeId)) return ServiceResult.Invalid("Select a valid leave type.");
        var balanceError = await CheckAllowance(employee.EmployeeId, dto.LeaveTypeId, dto.FromDate, dto.ToDate);
        if (balanceError != null) return ServiceResult.Conflict(balanceError);
        if (await _context.LeaveRequests.AnyAsync(l => l.EmployeeId == employee.EmployeeId && (l.Status == "Pending" || l.Status == "Approved") && l.FromDate < dto.ToDate.Date.AddDays(1) && l.ToDate >= dto.FromDate.Date))
            return ServiceResult.Conflict("These dates overlap an existing pending or approved request.");
        var leave = new Models.LeaveRequest { EmployeeId = employee.EmployeeId, LeaveTypeId = dto.LeaveTypeId, FromDate = dto.FromDate.Date, ToDate = dto.ToDate.Date, Reason = dto.Reason?.Trim(), Status = "Pending", CreatedAt = DateTime.UtcNow };
        _context.LeaveRequests.Add(leave);
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return ServiceResult.Success(new { leave.LeaveRequestId });
    }

    public Task<ServiceResult> ApproveLeave(int id, LeaveReviewDto dto) => Review(id, dto, "Approved");

    public Task<ServiceResult> RejectLeave(int id, LeaveReviewDto dto) => Review(id, dto, "Rejected");
    private async Task<ServiceResult> Review(int id, LeaveReviewDto dto, string status)
    {
        if (string.IsNullOrWhiteSpace(dto.Comment)) return ServiceResult.Invalid("Enter a review comment.");
        using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var leave = await _context.LeaveRequests.Include(l => l.Employee).FirstOrDefaultAsync(l => l.LeaveRequestId == id);
        if (leave == null) return ServiceResult.Missing();
        if (leave.Employee?.UserId == UserId) return ServiceResult.Invalid("You cannot review your own leave request.");
        if (leave.Status != "Pending") return ServiceResult.Conflict("This request has already been reviewed. Reload the list.");
        if (status == "Approved")
        {
            var balanceError = await CheckAllowance(leave.EmployeeId, leave.LeaveTypeId, leave.FromDate, leave.ToDate, leave.LeaveRequestId);
            if (balanceError != null) return ServiceResult.Conflict(balanceError);
        }
        leave.Status = status; leave.ReviewComment = dto.Comment.Trim(); leave.ApprovedBy = UserId;
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return ServiceResult.Success(new { leave.Status });
    }

    private async Task<string?> CheckAllowance(int employeeId, int typeId, DateTime from, DateTime to, int excludeId = 0)
    {
        var type = await _context.LeaveTypes.FindAsync(typeId);
        if (type == null) return "Leave type no longer exists.";
        var reserved = await _context.LeaveRequests.AsNoTracking().Where(l => l.EmployeeId == employeeId && l.LeaveTypeId == typeId && l.LeaveRequestId != excludeId && (l.Status == "Pending" || l.Status == "Approved") && l.FromDate < new DateTime(to.Year, 12, 31).AddDays(1) && l.ToDate >= new DateTime(from.Year, 1, 1)).ToListAsync();
        for (var year = from.Year; year <= to.Year; year++)
        {
            var used = reserved.Sum(l => LeaveBalanceCalculator.DaysInYear(l.FromDate, l.ToDate, year));
            var requested = LeaveBalanceCalculator.DaysInYear(from, to, year);
            if (used + requested > type.MaxDays)
                return $"Insufficient {type.LeaveTypeName} balance for {year}: {Math.Max(0, type.MaxDays - used)} days available, {requested} requested. Pending requests reserve days.";
        }
        return null;
    }

    public async Task<ServiceResult> GetBalances(int? year)
    {
        var selectedYear = year ?? DateTime.Today.Year;
        if (selectedYear < 1 || selectedYear > 9998) return ServiceResult.Invalid("Select a valid year.");
        var start = new DateTime(selectedYear, 1, 1);
        var end = start.AddYears(1);
        var employees = await _context.Employees.AsNoTracking().Where(e => Manager || e.UserId == UserId)
            .Select(e => new { e.EmployeeId, e.EmployeeCode, fullName = e.User == null ? null : e.User.FullName }).ToListAsync();
        var ids = employees.Select(e => e.EmployeeId).ToList();
        var types = await _context.LeaveTypes.AsNoTracking().OrderBy(t => t.LeaveTypeName).ToListAsync();
        var requests = await _context.LeaveRequests.AsNoTracking().Where(l => ids.Contains(l.EmployeeId) && (l.Status == "Pending" || l.Status == "Approved") && l.FromDate < end && l.ToDate >= start).ToListAsync();
        var result = employees.SelectMany(e => types.Select(t =>
        {
            var relevant = requests.Where(l => l.EmployeeId == e.EmployeeId && l.LeaveTypeId == t.LeaveTypeId).ToList();
            var used = relevant.Where(l => l.Status == "Approved").Sum(l => LeaveBalanceCalculator.DaysInYear(l.FromDate, l.ToDate, selectedYear));
            var pending = relevant.Where(l => l.Status == "Pending").Sum(l => LeaveBalanceCalculator.DaysInYear(l.FromDate, l.ToDate, selectedYear));
            return new { e.EmployeeId, e.EmployeeCode, employeeName = e.fullName, t.LeaveTypeId, t.LeaveTypeName, year = selectedYear, allowed = t.MaxDays, used, pending, remaining = t.MaxDays - used - pending };
        }));
        return ServiceResult.Success(result);
    }
}
