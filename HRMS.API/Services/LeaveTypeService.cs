using HRMS.API.Data;
using HRMS.API.Models;
using Microsoft.EntityFrameworkCore;

namespace HRMS.API.Services
{

    public sealed class LeaveTypeService : ILeaveTypeService
    {
        private readonly ApplicationDbContext _context;

        public LeaveTypeService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<ServiceResult> GetLeaveTypes()
        {
            return ServiceResult.Success(await _context.LeaveTypes.ToListAsync());
        }

        public async Task<ServiceResult> CreateLeaveType(LeaveType leaveType)
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

            return ServiceResult.Success(leaveType);
        }

        public async Task<ServiceResult> UpdateLeaveType(int id, LeaveType value)
        {
            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var existing = await _context.LeaveTypes.FindAsync(id);
            if (existing == null) return ServiceResult.Missing();
            var error = await Validate(value, id);
            if (error != null) return error;
            existing.LeaveTypeName = value.LeaveTypeName.Trim();
            existing.MaxDays = value.MaxDays;
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return ServiceResult.Success(existing);
        }

        private async Task<ServiceResult?> Validate(LeaveType value, int id)
        {
            if (string.IsNullOrWhiteSpace(value.LeaveTypeName)) return ServiceResult.Invalid("Enter a leave type name.");
            if (value.MaxDays < 1) return ServiceResult.Invalid("Maximum days must be a positive whole number.");
            var name = value.LeaveTypeName.Trim().ToUpper();
            if (await _context.LeaveTypes.AnyAsync(t => t.LeaveTypeId != id && t.LeaveTypeName.Trim().ToUpper() == name))
                return ServiceResult.Conflict("This leave type already exists.");
            return null;
        }
    }
}
