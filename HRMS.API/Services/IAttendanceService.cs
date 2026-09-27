using HRMS.API.DTOs;
using HRMS.API.Models;

namespace HRMS.API.Services;

public interface IAttendanceService
{
    Task<ServiceResult> GetAttendance();
    Task<ServiceResult> GetAttendanceById(int id);
    Task<ServiceResult> CheckIn();
    Task<ServiceResult> CheckOut(int attendanceId);
    Task<ServiceResult> DeleteAttendance(int id);
}
