using HRMS.API.DTOs;
using HRMS.API.Models;

namespace HRMS.API.Services;

public interface IAuthService
{
    Task<ServiceResult> Register(RegisterDto dto);
    Task<ServiceResult> RegisterEmployee(RegisterDto dto);
    Task<ServiceResult> ChangePassword(ChangePasswordDto dto);
    Task<ServiceResult> ResetPassword(int employeeId, ResetPasswordDto dto);
    Task<ServiceResult> Login(LoginDto dto);
    Task<ServiceResult> GetSession();
}
