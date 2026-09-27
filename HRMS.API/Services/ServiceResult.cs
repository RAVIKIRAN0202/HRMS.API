namespace HRMS.API.Services;

// Application outcomes; HTTP status codes are mapped only at the controller boundary.
public enum ServiceOutcome { Success, Created, Invalid, Missing, Conflict, Unauthenticated, Forbidden }

public sealed record ServiceResult(ServiceOutcome Outcome, object? Value = null, int? ResourceId = null)
{
    public static ServiceResult Success(object? value = null) => new(ServiceOutcome.Success, value);
    public static ServiceResult Created(int id, object value) => new(ServiceOutcome.Created, value, id);
    public static ServiceResult Invalid(object? value = null) => new(ServiceOutcome.Invalid, value);
    public static ServiceResult Missing(object? value = null) => new(ServiceOutcome.Missing, value);
    public static ServiceResult Conflict(object? value = null) => new(ServiceOutcome.Conflict, value);
    public static ServiceResult Unauthenticated(object? value = null) => new(ServiceOutcome.Unauthenticated, value);
    public static ServiceResult Forbidden() => new(ServiceOutcome.Forbidden);
}
