using HRMS.API.Data;
using HRMS.API.Controllers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
var config = new ConfigurationBuilder().AddJsonFile(Path.GetFullPath("HRMS.API/appsettings.json")).Build();
using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(config.GetConnectionString("DefaultConnection")).Options);
var id = await db.Users.Where(u => u.Role != null && u.Role.RoleName == "Employee").Select(u => u.UserId).FirstAsync();
var controller = new LeaveRequestController(db) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, id.ToString()), new Claim(ClaimTypes.Role, "Employee") }, "probe"));
try { var result = await controller.GetAllLeaveRequests(); Console.WriteLine("Employee leave query succeeded: " + result.GetType().Name); }
catch (Exception ex) { Console.WriteLine(ex.GetType().Name + ": " + ex.Message); }
var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(config["Jwt:Issuer"], config["Jwt:Audience"], new[] { new Claim(ClaimTypes.NameIdentifier, id.ToString()), new Claim(ClaimTypes.Role, "Employee") }, expires: DateTime.UtcNow.AddMinutes(2), signingCredentials: new Microsoft.IdentityModel.Tokens.SigningCredentials(new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(config["Jwt:Key"]!)), Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256));
using var client = new HttpClient(new HttpClientHandler { ServerCertificateCustomValidationCallback = (request, cert, chain, errors) => request.RequestUri!.Host == "localhost" });
client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token));
foreach (var path in new[] { "LeaveRequest/balances" }) {
    using var response = await client.GetAsync("https://localhost:7061/api/" + path);
    Console.WriteLine(path + " HTTP " + (int)response.StatusCode);
}
