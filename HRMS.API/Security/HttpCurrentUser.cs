using System.Security.Claims;
using HRMS.API.Services;

namespace HRMS.API.Security;

public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public string? Subject => accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
    public string? SessionVersion => accessor.HttpContext?.User.FindFirstValue("session_version");
    public bool IsInRole(string role) => accessor.HttpContext?.User.IsInRole(role) ?? false;
}
