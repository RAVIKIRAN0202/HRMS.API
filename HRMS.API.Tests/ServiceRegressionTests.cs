using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using HRMS.API.Controllers;
using HRMS.API.Data;
using HRMS.API.DTOs;
using HRMS.API.Models;
using HRMS.API.Security;
using HRMS.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HRMS.API.Tests;

public sealed class ServiceRegressionTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly ApplicationDbContext _db;
    private readonly Caller _caller = new();
    public ServiceRegressionTests()
    {
        _connection.Open();
        _db = new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
        _db.Roles.Add(new Role { RoleId = 1, RoleName = "Employee" });
        _db.Departments.Add(new Department { DepartmentId = 1, DepartmentName = "Engineering" });
        _db.Designations.Add(new Designation { DesignationId = 1, DepartmentId = 1, DesignationName = "Engineer" });
        for (var id = 1; id <= 2; id++)
        {
            _db.Users.Add(new User { UserId = id, FullName = $"Employee {id}", Email = $"employee{id}@example.test", RoleId = 1, IsActive = true, PasswordHash = BCrypt.Net.BCrypt.HashPassword("OldPassword1!", 4) });
            _db.Employees.Add(new Employee { EmployeeId = id, UserId = id, EmployeeCode = $"EMP{id}", DepartmentId = 1, DesignationId = 1, DateOfJoining = new DateTime(2020, 1, 1) });
        }
        _db.LeaveTypes.Add(new LeaveType { LeaveTypeId = 1, LeaveTypeName = "Annual", MaxDays = 10 });
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }

    private LeaveRequestService Leaves => new(_db, _caller);
    private static JsonElement Json(object? value) => JsonSerializer.SerializeToElement(value);
    private void AddLeave(int employeeId, DateTime from, DateTime to, string status = "Pending")
    {
        _db.LeaveRequests.Add(new LeaveRequest { EmployeeId = employeeId, LeaveTypeId = 1, FromDate = from, ToDate = to, Status = status });
        _db.SaveChanges();
    }

    [Fact]
    public async Task PendingRequestsReserveAllowanceWithoutCreatingAnotherRequest()
    {
        AddLeave(1, new(2027, 1, 1), new(2027, 1, 8));
        var result = await Leaves.ApplyLeave(new() { LeaveTypeId = 1, FromDate = new(2027, 2, 1), ToDate = new(2027, 2, 3) });
        Assert.Equal(ServiceOutcome.Conflict, result.Outcome);
        Assert.Contains("2 days available", Assert.IsType<string>(result.Value));
        Assert.Equal(1, await _db.LeaveRequests.CountAsync());
    }

    [Fact]
    public async Task OverlappingRequestIsRejected()
    {
        AddLeave(1, new(2027, 1, 1), new(2027, 1, 2));
        var result = await Leaves.ApplyLeave(new() { LeaveTypeId = 1, FromDate = new(2027, 1, 2), ToDate = new(2027, 1, 3) });
        Assert.Equal(ServiceOutcome.Conflict, result.Outcome);
        Assert.Contains("overlap", Assert.IsType<string>(result.Value));
    }

    [Fact]
    public async Task ManagerCannotApproveOwnRequestAndOtherManagerCannotReviewTwice()
    {
        AddLeave(1, new(2027, 3, 1), new(2027, 3, 2));
        var id = await _db.LeaveRequests.Select(x => x.LeaveRequestId).SingleAsync();
        _caller.Role = "HR";
        Assert.Equal(ServiceOutcome.Invalid, (await Leaves.ApproveLeave(id, new() { Comment = "Reviewed" })).Outcome);
        _caller.Subject = "2";
        Assert.Equal(ServiceOutcome.Success, (await Leaves.ApproveLeave(id, new() { Comment = " Reviewed " })).Outcome);
        Assert.Equal(ServiceOutcome.Conflict, (await Leaves.RejectLeave(id, new() { Comment = "Again" })).Outcome);
        var saved = await _db.LeaveRequests.SingleAsync();
        Assert.Equal("Approved", saved.Status);
        Assert.Equal("Reviewed", saved.ReviewComment);
        Assert.Equal(2, saved.ApprovedBy);
    }

    [Fact]
    public async Task BalancesSplitCalendarYearsAndRespectEmployeeVisibility()
    {
        AddLeave(1, new(2026, 12, 30), new(2027, 1, 2), "Approved");
        AddLeave(2, new(2027, 1, 1), new(2027, 1, 5));
        var balances = Json((await Leaves.GetBalances(2027)).Value);
        Assert.Equal(1, balances.GetArrayLength());
        Assert.Equal(2, balances[0].GetProperty("used").GetInt32());
        Assert.Equal(8, balances[0].GetProperty("remaining").GetInt32());
        Assert.Equal(1, Json((await Leaves.GetAllLeaveRequests()).Value).GetArrayLength());
        _caller.Role = "HR";
        Assert.Equal(2, Json((await Leaves.GetBalances(2027)).Value).GetArrayLength());
    }

    [Fact]
    public async Task CheckInRejectsDuplicateAndCheckOutRequiresOwner()
    {
        var service = new AttendanceService(_db, _caller);
        var first = await service.CheckIn();
        Assert.Equal(ServiceOutcome.Success, first.Outcome);
        var attendance = Assert.IsType<Attendance>(first.Value);
        Assert.Equal(ServiceOutcome.Invalid, (await service.CheckIn()).Outcome);
        _caller.Subject = "2";
        Assert.Equal(ServiceOutcome.Missing, (await service.CheckOut(attendance.AttendanceId)).Outcome);
        Assert.Equal(ServiceOutcome.Forbidden, (await service.GetAttendanceById(attendance.AttendanceId)).Outcome);
        _caller.Subject = "1";
        Assert.Equal(ServiceOutcome.Success, (await service.CheckOut(attendance.AttendanceId)).Outcome);
        Assert.Equal(ServiceOutcome.Invalid, (await service.CheckOut(attendance.AttendanceId)).Outcome);
    }

    [Fact]
    public async Task FailedOnboardingRollsBackNewLoginAccount()
    {
        var service = new EmployeeService(_db, _caller);
        var result = await service.Onboard(new() { FullName = "New Person", Email = "new@example.test", Password = "Password123!", EmployeeCode = "EMP3", DepartmentId = 999, DesignationId = 1, DateOfJoining = DateTime.Today });
        Assert.Equal(ServiceOutcome.Invalid, result.Outcome);
        _db.ChangeTracker.Clear();
        Assert.Equal(2, await _db.Users.CountAsync());
        Assert.Equal(2, await _db.Employees.CountAsync());
    }

    [Fact]
    public async Task EmployeeDetailsAreOwnerScopedAndExcludePasswordHash()
    {
        var service = new EmployeeService(_db, _caller);
        Assert.Equal(ServiceOutcome.Forbidden, (await service.GetEmployee(2)).Outcome);
        var result = await service.GetEmployee(1);
        Assert.Equal(ServiceOutcome.Success, result.Outcome);
        Assert.False(Json(result.Value).GetProperty("user").TryGetProperty("PasswordHash", out _));
    }

    [Fact]
    public async Task PasswordChangeIncrementsSessionAndRejectsOldSession()
    {
        var tokens = new Tokens();
        var service = new AuthService(_db, _caller, tokens);
        var result = await service.ChangePassword(new() { CurrentPassword = "OldPassword1!", NewPassword = "NewPassword2!" });
        Assert.Equal(ServiceOutcome.Success, result.Outcome);
        var user = await _db.Users.FindAsync(1);
        Assert.Equal(1, user!.SessionVersion);
        Assert.False(user.MustChangePassword);
        Assert.True(BCrypt.Net.BCrypt.Verify("NewPassword2!", user.PasswordHash));
        Assert.Equal("session-1", Json(result.Value).GetProperty("token").GetString());
        Assert.Equal(ServiceOutcome.Unauthenticated, (await service.ChangePassword(new() { CurrentPassword = "NewPassword2!", NewPassword = "AnotherPassword3!" })).Outcome);
    }

    [Fact]
    public async Task ResetPasswordInvalidatesSessionsAndRequiresChange()
    {
        var service = new AuthService(_db, _caller, new Tokens());
        Assert.Equal(ServiceOutcome.Success, (await service.ResetPassword(1, new() { TemporaryPassword = "Temporary123!" })).Outcome);
        var user = await _db.Users.FindAsync(1);
        Assert.Equal(1, user!.SessionVersion);
        Assert.True(user.MustChangePassword);
        Assert.True(BCrypt.Net.BCrypt.Verify("Temporary123!", user.PasswordHash));
    }

    [Fact]
    public async Task LoginRetainsResponseShapeAndRejectsInactiveAccount()
    {
        var service = new AuthService(_db, _caller, new Tokens());
        var result = await service.Login(new() { Email = " EMPLOYEE1@EXAMPLE.TEST ", Password = "OldPassword1!" });
        Assert.Equal(ServiceOutcome.Success, result.Outcome);
        Assert.Equal("Employee", Json(result.Value).GetProperty("user").GetProperty("role").GetString());
        var user = await _db.Users.FindAsync(1);
        user!.IsActive = false;
        await _db.SaveChangesAsync();
        Assert.Equal(ServiceOutcome.Unauthenticated, (await service.Login(new() { Email = user.Email, Password = "OldPassword1!" })).Outcome);
    }

    [Fact]
    public async Task DuplicateDepartmentAndDesignationNamesRemainRejected()
    {
        Assert.Equal(ServiceOutcome.Conflict, (await new DepartmentService(_db).CreateDepartment(new() { DepartmentName = " engineering " })).Outcome);
        Assert.Equal(ServiceOutcome.Conflict, (await new DesignationService(_db).CreateDesignation(new() { DepartmentId = 1, DesignationName = " engineer " })).Outcome);
        Assert.Equal(ServiceOutcome.Invalid, (await new LeaveTypeService(_db).CreateLeaveType(new() { LeaveTypeName = "Invalid", MaxDays = 0 })).Outcome);
    }

    [Fact]
    public void ResultMappingPreservesHttpStatusesAndCreatedLocationData()
    {
        var controller = new TestController();
        Assert.IsType<OkResult>(controller.ToActionResult(ServiceResult.Success()));
        Assert.IsType<BadRequestResult>(controller.ToActionResult(ServiceResult.Invalid()));
        Assert.IsType<NotFoundResult>(controller.ToActionResult(ServiceResult.Missing()));
        Assert.IsType<UnauthorizedResult>(controller.ToActionResult(ServiceResult.Unauthenticated()));
        Assert.IsType<ForbidResult>(controller.ToActionResult(ServiceResult.Forbidden()));
        Assert.Equal("invalid", Assert.IsType<BadRequestObjectResult>(controller.ToActionResult(ServiceResult.Invalid("invalid"))).Value);
        Assert.Equal(409, Assert.IsType<ConflictObjectResult>(controller.ToActionResult(ServiceResult.Conflict("duplicate"))).StatusCode);
        var created = Assert.IsType<CreatedAtActionResult>(controller.ToActionResult(ServiceResult.Created(42, new { EmployeeId = 42 }), "GetEmployee"));
        Assert.Equal(201, created.StatusCode);
        Assert.Equal("GetEmployee", created.ActionName);
        Assert.Equal(42, created.RouteValues!["id"]);
    }

    [Fact]
    public void DependencyInjectionResolvesControllersAndServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddDbContext<ApplicationDbContext>(o => o.UseSqlite(_connection));
        services.AddHrmsServices();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        using var scope = provider.CreateScope();
        foreach (var type in typeof(AuthController).Assembly.GetTypes().Where(t => !t.IsAbstract && t.IsSubclassOf(typeof(ControllerBase))))
            Assert.NotNull(ActivatorUtilities.CreateInstance(scope.ServiceProvider, type));
    }

    [Fact]
    public void CurrentUserAdapterReadsAuthenticatedClaimsAndJwtRetainsSessionClaims()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "7"), new Claim(ClaimTypes.Role, "HR"), new Claim("session_version", "3") }, "test"));
        var current = new HttpCurrentUser(new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = principal } });
        Assert.Equal("7", current.Subject);
        Assert.Equal("3", current.SessionVersion);
        Assert.True(current.IsInRole("HR"));
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:Key"] = new string('x', 64), ["Jwt:Issuer"] = "test", ["Jwt:Audience"] = "test-client" }).Build();
        var encoded = new JwtTokenGenerator(config).Generate(new User { UserId = 7, FullName = "Test", Email = "test@example.test", Role = new Role { RoleName = "HR" }, SessionVersion = 3, MustChangePassword = true });
        var token = new JwtSecurityTokenHandler().ReadJwtToken(encoded);
        Assert.Contains(token.Claims, c => c.Type == "session_version" && c.Value == "3");
        Assert.Contains(token.Claims, c => c.Type == "must_change_password" && c.Value == "true");
        Assert.Equal("test", token.Issuer);
    }

    public void Dispose() { _db.Dispose(); _connection.Dispose(); }
    private sealed class TestController : ControllerBase { }
    private sealed class Tokens : ITokenGenerator { public string Generate(User user) => $"session-{user.SessionVersion}"; }
    private sealed class Caller : ICurrentUser
    {
        public string? Subject { get; set; } = "1";
        public string? SessionVersion => "0";
        public string Role { get; set; } = "Employee";
        public bool IsInRole(string role) => Role == role;
    }
}
