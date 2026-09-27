using HRMS.API.Data;
using HRMS.API.DTOs;
using HRMS.API.Models;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace HRMS.API.Services
{

    public sealed class AuthService : IAuthService
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentUser _currentUser;
        private readonly ITokenGenerator _tokens;

        public AuthService(ApplicationDbContext context, ICurrentUser currentUser, ITokenGenerator tokens)
        {
            _context = context;
            _currentUser = currentUser;
            _tokens = tokens;
        }

        public async Task<ServiceResult> Register(RegisterDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.FullName) || string.IsNullOrWhiteSpace(dto.Email) || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(dto.Email.Trim()))
                return ServiceResult.Invalid("Enter a name and valid email address.");
            if (string.IsNullOrWhiteSpace(dto.Password) || dto.Password.Length < 8 || Encoding.UTF8.GetByteCount(dto.Password) > 72)
                return ServiceResult.Invalid("Password must contain at least 8 characters and no more than 72 UTF-8 bytes.");
            if (!await _context.Roles.AnyAsync(r => r.RoleId == dto.RoleId)) return ServiceResult.Invalid("Select a valid role.");
            dto.Email = dto.Email.Trim();
            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var emailExists = await _context.Users.AnyAsync(u => u.Email.Trim().ToUpper() == dto.Email.ToUpper());

            if (emailExists)
                return ServiceResult.Invalid("Email already exists");

            var user = new User
            {
                FullName = dto.FullName.Trim(),
                Email = dto.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                RoleId = dto.RoleId,
                IsActive = true,
                CreatedAt = DateTime.Now
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return ServiceResult.Success("User registered successfully");
        }

        public async Task<ServiceResult> RegisterEmployee(RegisterDto dto)
        {
            var role = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == "Employee");
            if (role == null) return ServiceResult.Invalid("The Employee role is not configured. Please contact your administrator.");
            dto.RoleId = role.RoleId;
            return await Register(dto);
        }

        public async Task<ServiceResult> ChangePassword(ChangePasswordDto dto)
        {
            if (!int.TryParse(_currentUser.Subject, out var id)) return ServiceResult.Unauthenticated();
            if (string.IsNullOrWhiteSpace(dto.NewPassword) || dto.NewPassword.Length < 8 || Encoding.UTF8.GetByteCount(dto.NewPassword) > 72)
                return ServiceResult.Invalid("Password must contain at least 8 characters and no more than 72 UTF-8 bytes.");
            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var user = await _context.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.UserId == id);
            if (user == null || !user.IsActive) return ServiceResult.Unauthenticated();
            if (_currentUser.SessionVersion != user.SessionVersion.ToString()) return ServiceResult.Unauthenticated();
            if (string.IsNullOrEmpty(dto.CurrentPassword) || !BCrypt.Net.BCrypt.Verify(dto.CurrentPassword, user.PasswordHash))
                return ServiceResult.Invalid("Current password is incorrect.");
            if (dto.CurrentPassword == dto.NewPassword) return ServiceResult.Invalid("Choose a different new password.");
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
            user.MustChangePassword = false;
            user.SessionVersion++;
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return ServiceResult.Success(new { token = _tokens.Generate(user), mustChangePassword = false });
        }

        public async Task<ServiceResult> ResetPassword(int employeeId, ResetPasswordDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.TemporaryPassword) || dto.TemporaryPassword.Length < 8 || Encoding.UTF8.GetByteCount(dto.TemporaryPassword) > 72)
                return ServiceResult.Invalid("Temporary password must contain at least 8 characters and no more than 72 UTF-8 bytes.");
            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var employee = await _context.Employees.Include(e => e.User).ThenInclude(u => u!.Role).FirstOrDefaultAsync(e => e.EmployeeId == employeeId);
            var user = employee?.User;
            if (user == null) return ServiceResult.Missing("Employee account not found.");
            if (user.Role?.RoleName != "Employee") return ServiceResult.Invalid("Only Employee-role accounts can be reset here.");
            if (!user.IsActive) return ServiceResult.Invalid("This login account is inactive.");
            if (BCrypt.Net.BCrypt.Verify(dto.TemporaryPassword, user.PasswordHash)) return ServiceResult.Invalid("Choose a different temporary password.");
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.TemporaryPassword);
            user.MustChangePassword = true;
            user.SessionVersion++;
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return ServiceResult.Success("Password reset. Existing sessions are invalidated.");
        }

        public async Task<ServiceResult> Login(LoginDto dto)
        {
            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Email.ToUpper() == dto.Email.Trim().ToUpper());

            if (user == null || !user.IsActive)
                return ServiceResult.Unauthenticated("Invalid email or password");

            bool isPasswordValid = BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash);

            if (!isPasswordValid)
                return ServiceResult.Unauthenticated("Invalid email or password");

            var token = _tokens.Generate(user);

            return ServiceResult.Success(new
            {
                token = token,
                user = new
                {
                    user.UserId,
                    user.FullName,
                    user.Email,
                    user.MustChangePassword,
                    role = user.Role?.RoleName
                }
            });
        }

        public async Task<ServiceResult> GetSession()
        {
            if (!int.TryParse(_currentUser.Subject, out var id)) return ServiceResult.Unauthenticated();
            var user = await _context.Users.AsNoTracking().Where(u => u.UserId == id && u.IsActive)
                .Select(u => new { u.UserId, u.FullName, role = u.Role == null ? null : u.Role.RoleName, u.MustChangePassword }).FirstOrDefaultAsync();
            return user == null ? ServiceResult.Unauthenticated() : ServiceResult.Success(user);
        }
    }
}
