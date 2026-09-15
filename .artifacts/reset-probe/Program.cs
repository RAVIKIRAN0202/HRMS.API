using HRMS.API.Data;
using HRMS.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Net.Http.Json;
using System.Text.Json;
var config = new ConfigurationBuilder().AddJsonFile(Path.GetFullPath("HRMS.API/appsettings.json")).Build();
using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(config.GetConnectionString("DefaultConnection")).Options);
var admin = await db.Users.Include(u => u.Role).FirstAsync(u => u.IsActive && u.Role != null && u.Role.RoleName == "Admin");
var employeeRole = await db.Roles.FirstAsync(r => r.RoleName == "Employee");
var designation = await db.Designations.FirstAsync(d => d.IsActive && d.Department != null && d.Department.IsActive);
var marker = Guid.NewGuid().ToString("N");
var original = "Start-" + Guid.NewGuid().ToString("N");
var temporary = "Temp-" + Guid.NewGuid().ToString("N");
var permanent = "Final-" + Guid.NewGuid().ToString("N");
var user = new User { FullName = "Password reset integration test", Email = marker + "@example.invalid", PasswordHash = BCrypt.Net.BCrypt.HashPassword(original), RoleId = employeeRole.RoleId, IsActive = true, CreatedAt = DateTime.UtcNow };
Employee? employee = null;
LeaveType? testLeaveType = null;
using var client = new HttpClient(new HttpClientHandler { ServerCertificateCustomValidationCallback = (request, cert, chain, errors) => request.RequestUri!.Host == "localhost" }) { BaseAddress = new Uri("https://localhost:7061/api/") };
string Token(User u, string role) => new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(config["Jwt:Issuer"], config["Jwt:Audience"], new[] { new Claim(ClaimTypes.NameIdentifier, u.UserId.ToString()), new Claim(ClaimTypes.Role, role), new Claim("session_version", u.SessionVersion.ToString()) }, expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new SigningCredentials(new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(config["Jwt:Key"]!)), SecurityAlgorithms.HmacSha256)));
void Use(string token) => client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
async Task Check(HttpResponseMessage response, int expected, string label) { using (response) { if ((int)response.StatusCode != expected) throw new Exception(label + " returned " + (int)response.StatusCode); Console.WriteLine(label + " passed"); await Task.CompletedTask; } }
async Task<string> Login(string password) {
    client.DefaultRequestHeaders.Authorization = null;
    using var response = await client.PostAsJsonAsync("Auth/login", new { email = user.Email, password });
    response.EnsureSuccessStatusCode();
    var json = await response.Content.ReadFromJsonAsync<JsonElement>();
    return json.GetProperty("token").GetString()!;
}
try {
    await Check(await client.GetAsync("Employee"), 401, "Anonymous employee access rejected");
    Use(Token(admin, "Admin"));
    await Check(await client.PostAsJsonAsync("Employee/onboard", new { fullName = user.FullName, email = user.Email, password = original, employeeCode = "RST" + marker[..8], departmentId = designation.DepartmentId, designationId = designation.DesignationId, dateOfJoining = DateTime.Today, salary = 1000, status = "Active" }), 201, "Admin employee onboarding");
    user = await db.Users.SingleAsync(u => u.Email == marker + "@example.invalid");
    employee = await db.Employees.SingleAsync(e => e.UserId == user.UserId);
    testLeaveType = new LeaveType { LeaveTypeName = "TEST-" + marker[..8], MaxDays = 3, CreatedAt = DateTime.UtcNow };
    db.LeaveTypes.Add(testLeaveType); await db.SaveChangesAsync();
    var oldToken = await Login(original);
    Use(oldToken);
    await Check(await client.GetAsync("Auth/session"), 200, "Employee session validation");
    foreach (var route in new[] { "Employee", "Department", "Designation", "Dashboard/summary" })
        await Check(await client.GetAsync(route), 403, "Employee denied " + route);
    await Check(await client.PutAsync("Attendance/check-out/2147483647", null), 404, "Checkout without record rejected");
    using var checkin = await client.PostAsync("Attendance/check-in", null);
    checkin.EnsureSuccessStatusCode();
    var attendanceId = (await checkin.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("attendanceId").GetInt32();
    await Check(await client.PostAsync("Attendance/check-in", null), 400, "Duplicate check-in rejected");
    await Check(await client.PutAsync("Attendance/check-out/" + attendanceId, null), 200, "Employee checkout");
    await Check(await client.PutAsync("Attendance/check-out/" + attendanceId, null), 400, "Duplicate checkout rejected");
    var leaveDate = new DateTime(DateTime.Today.Year + 1, 1, 10);
    using var applied = await client.PostAsJsonAsync("LeaveRequest", new { leaveTypeId = testLeaveType.LeaveTypeId, fromDate = leaveDate, toDate = leaveDate.AddDays(1), reason = "Integration test" });
    applied.EnsureSuccessStatusCode();
    var leaveId = (await applied.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("leaveRequestId").GetInt32();
    async Task Balance(int used, int pending, int remaining) {
        using var response = await client.GetAsync("LeaveRequest/balances?year=" + leaveDate.Year);
        response.EnsureSuccessStatusCode();
        var rows = await response.Content.ReadFromJsonAsync<JsonElement>();
        var row = rows.EnumerateArray().Single(r => r.GetProperty("employeeId").GetInt32() == employee.EmployeeId && r.GetProperty("leaveTypeId").GetInt32() == testLeaveType.LeaveTypeId);
        if (row.GetProperty("used").GetInt32() != used || row.GetProperty("pending").GetInt32() != pending || row.GetProperty("remaining").GetInt32() != remaining) throw new Exception("Incorrect balance");
        Console.WriteLine("Balance verified: used=" + used + ", pending=" + pending + ", remaining=" + remaining);
    }
    await Balance(0, 2, 1);
    await Check(await client.PostAsJsonAsync("LeaveRequest", new { leaveTypeId = testLeaveType.LeaveTypeId, fromDate = leaveDate, toDate = leaveDate }), 409, "Overlapping leave rejected");
    await Check(await client.PostAsJsonAsync("LeaveRequest", new { leaveTypeId = testLeaveType.LeaveTypeId, fromDate = leaveDate.AddDays(4), toDate = leaveDate.AddDays(5) }), 409, "Over-allowance leave rejected");
    await Check(await client.PutAsJsonAsync("LeaveRequest/approve/" + leaveId, new { comment = "Test" }), 403, "Employee cannot approve leave");
    Use(Token(admin, "HR"));
    await Check(await client.PutAsJsonAsync("LeaveRequest/approve/" + leaveId, new { comment = "Integration approval" }), 200, "HR leave approval");
    await Check(await client.PutAsJsonAsync("LeaveRequest/reject/" + leaveId, new { comment = "Test" }), 409, "Reviewed request cannot change again");
    Use(oldToken); await Balance(2, 0, 1);
    using var appliedAgain = await client.PostAsJsonAsync("LeaveRequest", new { leaveTypeId = testLeaveType.LeaveTypeId, fromDate = leaveDate.AddDays(4), toDate = leaveDate.AddDays(4) });
    appliedAgain.EnsureSuccessStatusCode();
    var rejectedId = (await appliedAgain.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("leaveRequestId").GetInt32();
    await Balance(2, 1, 0);
    Use(Token(admin, "HR"));
    await Check(await client.PutAsJsonAsync("LeaveRequest/reject/" + rejectedId, new { comment = "Integration rejection" }), 200, "HR leave rejection");
    Use(oldToken); await Balance(2, 0, 1);
    await Check(await client.PostAsJsonAsync($"Auth/reset-password/{employee.EmployeeId}", new { temporaryPassword = temporary }), 403, "Employee cannot reset passwords");
    Use(Token(admin, "HR"));
    await Check(await client.PostAsJsonAsync($"Auth/reset-password/{employee.EmployeeId}", new { temporaryPassword = temporary }), 403, "HR cannot reset passwords");
    Use(Token(admin, "Admin"));
    await Check(await client.PostAsJsonAsync($"Auth/reset-password/{employee.EmployeeId}", new { temporaryPassword = temporary }), 200, "Admin reset");
    Use(oldToken); await Check(await client.GetAsync("Dashboard/me"), 401, "Old session revoked");
    var tempToken = await Login(temporary);
    Use(tempToken); await Check(await client.GetAsync("Dashboard/me"), 403, "Temporary session blocked from data");
    await Check(await client.PostAsJsonAsync("Auth/change-password", new { currentPassword = original, newPassword = permanent }), 400, "Incorrect current password rejected");
    using var changed = await client.PostAsJsonAsync("Auth/change-password", new { currentPassword = temporary, newPassword = permanent });
    changed.EnsureSuccessStatusCode();
    var fresh = (await changed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;
    Use(tempToken); await Check(await client.GetAsync("Dashboard/me"), 401, "Temporary session revoked after change");
    Use(fresh); await Check(await client.GetAsync("Dashboard/me"), 200, "New session accesses personal data");
    Console.WriteLine("Password reset integration checks passed.");
} finally {
    if (employee?.EmployeeId > 0) {
        db.LeaveRequests.RemoveRange(await db.LeaveRequests.Where(l => l.EmployeeId == employee.EmployeeId).ToListAsync());
        db.Attendance.RemoveRange(await db.Attendance.Where(a => a.EmployeeId == employee.EmployeeId).ToListAsync());
        await db.SaveChangesAsync();
    }
    if (employee?.EmployeeId > 0) { db.Employees.Remove(employee); await db.SaveChangesAsync(); }
    if (user.UserId > 0) { db.Users.Remove(user); await db.SaveChangesAsync(); }
    if (testLeaveType?.LeaveTypeId > 0) { db.LeaveTypes.Remove(testLeaveType); await db.SaveChangesAsync(); }
    Console.WriteLine("Temporary test records removed.");
}
