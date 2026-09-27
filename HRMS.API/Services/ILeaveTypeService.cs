using HRMS.API.DTOs;
using HRMS.API.Models;

namespace HRMS.API.Services;

public interface ILeaveTypeService
{
    Task<ServiceResult> GetLeaveTypes();
    Task<ServiceResult> CreateLeaveType(LeaveType leaveType);
    Task<ServiceResult> UpdateLeaveType(int id, LeaveType value);
}
