using HRMS.API.Data;
using HRMS.API.Models;
using Microsoft.EntityFrameworkCore;

namespace HRMS.API.Services
{

    public sealed class DepartmentService : IDepartmentService
    {
        private readonly ApplicationDbContext _context;

        public DepartmentService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<ServiceResult> GetDepartments()
        {
            return ServiceResult.Success(await _context.Departments.ToListAsync());
        }

        public async Task<ServiceResult> GetDepartment(int id)
        {
            var department = await _context.Departments.FindAsync(id);

            if (department == null)
                return ServiceResult.Missing();

            return ServiceResult.Success(department);
        }

        public async Task<ServiceResult> CreateDepartment(Department department)
        {
            if (string.IsNullOrWhiteSpace(department.DepartmentName))
                return ServiceResult.Invalid("Enter a department name.");
            department.DepartmentName = department.DepartmentName.Trim();
            department.Description = department.Description?.Trim();
            department.DepartmentId = 0;
            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            if (await _context.Departments.AnyAsync(d => d.DepartmentName.Trim().ToUpper() == department.DepartmentName.ToUpper()))
                return ServiceResult.Conflict("A department with this name already exists.");
            _context.Departments.Add(department);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return ServiceResult.Success(department);
        }

        public async Task<ServiceResult> UpdateDepartment(int id, Department department)
        {
            if (id != department.DepartmentId)
                return ServiceResult.Invalid();

            if (string.IsNullOrWhiteSpace(department.DepartmentName))
                return ServiceResult.Invalid("Enter a department name.");
            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var existing = await _context.Departments.FindAsync(id);
            if (existing == null)
                return ServiceResult.Missing("Department no longer exists. Reload departments.");
            var name = department.DepartmentName.Trim();
            if (await _context.Departments.AnyAsync(d => d.DepartmentId != id && d.DepartmentName.Trim().ToUpper() == name.ToUpper()))
                return ServiceResult.Conflict("A department with this name already exists.");
            existing.DepartmentName = name;
            existing.Description = department.Description?.Trim();
            existing.IsActive = department.IsActive;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return ServiceResult.Success(existing);
        }

        public async Task<ServiceResult> DeleteDepartment(int id)
        {
            var department = await _context.Departments.FindAsync(id);

            if (department == null)
                return ServiceResult.Missing();

            _context.Departments.Remove(department);

            await _context.SaveChangesAsync();

            return ServiceResult.Success();
        }
    }
}
