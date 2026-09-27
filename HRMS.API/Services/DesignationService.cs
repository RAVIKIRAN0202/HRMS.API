using HRMS.API.Data;
using HRMS.API.Models;
using Microsoft.EntityFrameworkCore;

namespace HRMS.API.Services
{

    public sealed class DesignationService : IDesignationService
    {
        private readonly ApplicationDbContext _context;

        public DesignationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<ServiceResult> GetDesignations()
        {
            var designations = await _context.Designations
                .Include(d => d.Department)
                .ToListAsync();

            return ServiceResult.Success(designations);
        }

        public async Task<ServiceResult> GetDesignation(int id)
        {
            var designation = await _context.Designations
                .Include(d => d.Department)
                .FirstOrDefaultAsync(d => d.DesignationId == id);

            if (designation == null)
                return ServiceResult.Missing();

            return ServiceResult.Success(designation);
        }

        public async Task<ServiceResult> CreateDesignation(Designation designation)
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

            return ServiceResult.Success(designation);
        }

        public async Task<ServiceResult> UpdateDesignation(int id, Designation designation)
        {
            if (id != designation.DesignationId)
                return ServiceResult.Invalid();

            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var existing = await _context.Designations.FindAsync(id);
            if (existing == null) return ServiceResult.Missing();
            var error = await Validate(designation, existing);
            if (error != null) return error;
            if (existing.DepartmentId != designation.DepartmentId && await _context.Employees.AnyAsync(e => e.DesignationId == id))
                return ServiceResult.Invalid("This designation is assigned to employees. Create a new designation in the other department.");
            existing.DesignationName = designation.DesignationName.Trim();
            existing.DepartmentId = designation.DepartmentId;
            existing.IsActive = designation.IsActive;
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return ServiceResult.Success(designation);
        }

        public async Task<ServiceResult> DeleteDesignation(int id)
        {
            var designation = await _context.Designations.FindAsync(id);

            if (designation == null)
                return ServiceResult.Missing();

            _context.Designations.Remove(designation);
            await _context.SaveChangesAsync();

            return ServiceResult.Success();
        }

        private async Task<ServiceResult?> Validate(Designation value, Designation? existing = null)
        {
            if (string.IsNullOrWhiteSpace(value.DesignationName)) return ServiceResult.Invalid("Enter a designation name.");
            var previousDepartment = existing?.DepartmentId ?? 0;
            if (!await _context.Departments.AnyAsync(d => d.DepartmentId == value.DepartmentId && (d.IsActive || d.DepartmentId == previousDepartment)))
                return ServiceResult.Invalid("Select an active department.");
            var id = existing?.DesignationId ?? 0;
            var name = value.DesignationName.Trim().ToUpper();
            if (await _context.Designations.AnyAsync(d => d.DesignationId != id && d.DepartmentId == value.DepartmentId && d.DesignationName.Trim().ToUpper() == name))
                return ServiceResult.Conflict("This designation already exists in the department.");
            return null;
        }
    }
}
