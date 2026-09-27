using HRMS.API.DTOs;
using HRMS.API.Models;

namespace HRMS.API.Services;

public interface IDashboardService
{
    Task<ServiceResult> GetSummary();
    Task<ServiceResult> GetMyDashboard();
}
