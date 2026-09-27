using HRMS.API.DTOs;
using HRMS.API.Models;

namespace HRMS.API.Services;

public interface IDesignationService
{
    Task<ServiceResult> GetDesignations();
    Task<ServiceResult> GetDesignation(int id);
    Task<ServiceResult> CreateDesignation(Designation designation);
    Task<ServiceResult> UpdateDesignation(int id, Designation designation);
    Task<ServiceResult> DeleteDesignation(int id);
}
