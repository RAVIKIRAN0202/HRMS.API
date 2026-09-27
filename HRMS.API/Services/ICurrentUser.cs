namespace HRMS.API.Services;

// The web adapter reads the authenticated principal. Services never accept a caller ID from a request DTO.
public interface ICurrentUser
{
    string? Subject { get; }
    string? SessionVersion { get; }
    bool IsInRole(string role);
}
