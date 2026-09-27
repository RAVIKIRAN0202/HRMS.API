using HRMS.API.DTOs;
using HRMS.API.Models;

namespace HRMS.API.Services;

public interface IEmployeeService
{
    Task<ServiceResult> GetEmployees();
    Task<ServiceResult> GetAvailableUsers();
    Task<ServiceResult> GetEmployee(int id);
    Task<ServiceResult> CreateEmployee(EmployeeCreateDto dto);
    Task<ServiceResult> Onboard(EmployeeOnboardDto dto);
    Task<ServiceResult> UpdateEmployee(int id, EmployeeCreateDto dto);
    Task<ServiceResult> DeleteEmployee(int id);
}
