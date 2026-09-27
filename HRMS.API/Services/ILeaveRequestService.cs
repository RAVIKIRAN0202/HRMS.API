using HRMS.API.DTOs;
using HRMS.API.Models;

namespace HRMS.API.Services;

public interface ILeaveRequestService
{
    Task<ServiceResult> GetAllLeaveRequests();
    Task<ServiceResult> GetLeaveRequest(int id);
    Task<ServiceResult> ApplyLeave(LeaveRequestCreateDto dto);
    Task<ServiceResult> ApproveLeave(int id, LeaveReviewDto dto);
    Task<ServiceResult> RejectLeave(int id, LeaveReviewDto dto);
    Task<ServiceResult> GetBalances(int? year);
}
