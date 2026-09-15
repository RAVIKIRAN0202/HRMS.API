using HRMS.API.Data;
using HRMS.API.DTOs;
using HRMS.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace HRMS.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public AuthController(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        [HttpPost("register")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Register(RegisterDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.FullName) || string.IsNullOrWhiteSpace(dto.Email) || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(dto.Email.Trim()))
                return BadRequest("Enter a name and valid email address.");
            if (string.IsNullOrWhiteSpace(dto.Password) || dto.Password.Length < 8 || Encoding.UTF8.GetByteCount(dto.Password) > 72)
                return BadRequest("Password must contain at least 8 characters and no more than 72 UTF-8 bytes.");
            if (!await _context.Roles.AnyAsync(r => r.RoleId == dto.RoleId)) return BadRequest("Select a valid role.");
            dto.Email = dto.Email.Trim();
            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var emailExists = await _context.Users.AnyAsync(u => u.Email.Trim().ToUpper() == dto.Email.ToUpper());

            if (emailExists)
                return BadRequest("Email already exists");

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

            return Ok("User registered successfully");
        }

        [HttpPost("register-employee")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> RegisterEmployee(RegisterDto dto)
        {
            var role = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == "Employee");
            if (role == null) return BadRequest("The Employee role is not configured. Please contact your administrator.");
            dto.RoleId = role.RoleId;
            return await Register(dto);
        }

        [HttpPost("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword(ChangePasswordDto dto)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) return Unauthorized();
            if (string.IsNullOrWhiteSpace(dto.NewPassword) || dto.NewPassword.Length < 8 || Encoding.UTF8.GetByteCount(dto.NewPassword) > 72)
                return BadRequest("Password must contain at least 8 characters and no more than 72 UTF-8 bytes.");
            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var user = await _context.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.UserId == id);
            if (user == null || !user.IsActive) return Unauthorized();
            if (User.FindFirstValue("session_version") != user.SessionVersion.ToString()) return Unauthorized();
            if (string.IsNullOrEmpty(dto.CurrentPassword) || !BCrypt.Net.BCrypt.Verify(dto.CurrentPassword, user.PasswordHash))
                return BadRequest("Current password is incorrect.");
            if (dto.CurrentPassword == dto.NewPassword) return BadRequest("Choose a different new password.");
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
            user.MustChangePassword = false;
            user.SessionVersion++;
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return Ok(new { token = GenerateJwtToken(user), mustChangePassword = false });
        }

        [HttpPost("reset-password/{employeeId:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ResetPassword(int employeeId, ResetPasswordDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.TemporaryPassword) || dto.TemporaryPassword.Length < 8 || Encoding.UTF8.GetByteCount(dto.TemporaryPassword) > 72)
                return BadRequest("Temporary password must contain at least 8 characters and no more than 72 UTF-8 bytes.");
            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var employee = await _context.Employees.Include(e => e.User).ThenInclude(u => u!.Role).FirstOrDefaultAsync(e => e.EmployeeId == employeeId);
            var user = employee?.User;
            if (user == null) return NotFound("Employee account not found.");
            if (user.Role?.RoleName != "Employee") return BadRequest("Only Employee-role accounts can be reset here.");
            if (!user.IsActive) return BadRequest("This login account is inactive.");
            if (BCrypt.Net.BCrypt.Verify(dto.TemporaryPassword, user.PasswordHash)) return BadRequest("Choose a different temporary password.");
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.TemporaryPassword);
            user.MustChangePassword = true;
            user.SessionVersion++;
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return Ok("Password reset. Existing sessions are invalidated.");
        }

        [HttpPost("login")]
        [Microsoft.AspNetCore.Authorization.AllowAnonymous]
        public async Task<IActionResult> Login(LoginDto dto)
        {
            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Email.ToUpper() == dto.Email.Trim().ToUpper());

            if (user == null || !user.IsActive)
                return Unauthorized("Invalid email or password");

            bool isPasswordValid = BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash);

            if (!isPasswordValid)
                return Unauthorized("Invalid email or password");

            var token = GenerateJwtToken(user);

            return Ok(new
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

        private string GenerateJwtToken(User user)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role?.RoleName ?? ""),
                new Claim("session_version", user.SessionVersion.ToString()),
                new Claim("must_change_password", user.MustChangePassword ? "true" : "false")
            };

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));

            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.Now.AddHours(2),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        [HttpGet("session")]
        [Authorize]
        public async Task<IActionResult> GetSession()
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) return Unauthorized();
            var user = await _context.Users.AsNoTracking().Where(u => u.UserId == id && u.IsActive)
                .Select(u => new { u.UserId, u.FullName, role = u.Role == null ? null : u.Role.RoleName, u.MustChangePassword }).FirstOrDefaultAsync();
            return user == null ? Unauthorized() : Ok(user);
        }
    }
}
