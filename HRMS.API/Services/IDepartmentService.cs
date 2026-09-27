using HRMS.API.DTOs;
using HRMS.API.Models;

namespace HRMS.API.Services;

public interface IDepartmentService
{
    Task<ServiceResult> GetDepartments();
    Task<ServiceResult> GetDepartment(int id);
    Task<ServiceResult> CreateDepartment(Department department);
    Task<ServiceResult> UpdateDepartment(int id, Department department);
    Task<ServiceResult> DeleteDepartment(int id);
}
