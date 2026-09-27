using System.Reflection;
using HRMS.API.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace HRMS.API.Tests;

public class EndpointContractTests
{
    // Snapshot of all public endpoints before the service-layer refactor.
    [Theory]
    [InlineData(typeof(AttendanceController), "GetAttendance", "GET", null, "", "Admin,HR,Employee", false)]
    [InlineData(typeof(AttendanceController), "GetAttendanceById", "GET", "{id}", "", "Admin,HR,Employee", false)]
    [InlineData(typeof(AttendanceController), "CheckIn", "POST", "check-in", "", "Admin,HR,Employee", false)]
    [InlineData(typeof(AttendanceController), "CheckOut", "PUT", "check-out/{attendanceId}", "", "Admin,HR,Employee", false)]
    [InlineData(typeof(AttendanceController), "DeleteAttendance", "DELETE", "{id}", "", "Admin", false)]
    [InlineData(typeof(AuthController), "Register", "POST", "register", null, "Admin", false)]
    [InlineData(typeof(AuthController), "RegisterEmployee", "POST", "register-employee", null, "Admin", false)]
    [InlineData(typeof(AuthController), "ChangePassword", "POST", "change-password", null, "", false)]
    [InlineData(typeof(AuthController), "ResetPassword", "POST", "reset-password/{employeeId:int}", null, "Admin", false)]
    [InlineData(typeof(AuthController), "Login", "POST", "login", null, null, true)]
    [InlineData(typeof(AuthController), "GetSession", "GET", "session", null, "", false)]
    [InlineData(typeof(DashboardController), "GetSummary", "GET", "summary", "", "Admin,HR", false)]
    [InlineData(typeof(DashboardController), "GetMyDashboard", "GET", "me", "", "Employee", false)]
    [InlineData(typeof(DepartmentController), "GetDepartments", "GET", null, "Admin,HR", null, false)]
    [InlineData(typeof(DepartmentController), "GetDepartment", "GET", "{id}", "Admin,HR", null, false)]
    [InlineData(typeof(DepartmentController), "CreateDepartment", "POST", null, "Admin,HR", null, false)]
    [InlineData(typeof(DepartmentController), "UpdateDepartment", "PUT", "{id}", "Admin,HR", null, false)]
    [InlineData(typeof(DepartmentController), "DeleteDepartment", "DELETE", "{id}", "Admin,HR", null, false)]
    [InlineData(typeof(DesignationController), "GetDesignations", "GET", null, "Admin,HR", null, false)]
    [InlineData(typeof(DesignationController), "GetDesignation", "GET", "{id}", "Admin,HR", null, false)]
    [InlineData(typeof(DesignationController), "CreateDesignation", "POST", null, "Admin,HR", null, false)]
    [InlineData(typeof(DesignationController), "UpdateDesignation", "PUT", "{id}", "Admin,HR", null, false)]
    [InlineData(typeof(DesignationController), "DeleteDesignation", "DELETE", "{id}", "Admin,HR", null, false)]
    [InlineData(typeof(EmployeeController), "GetEmployees", "GET", null, "", "Admin,HR", false)]
    [InlineData(typeof(EmployeeController), "GetAvailableUsers", "GET", "users", "", "Admin,HR", false)]
    [InlineData(typeof(EmployeeController), "GetEmployee", "GET", "{id:int}", "", "Admin,HR,Employee", false)]
    [InlineData(typeof(EmployeeController), "CreateEmployee", "POST", null, "", "Admin,HR", false)]
    [InlineData(typeof(EmployeeController), "Onboard", "POST", "onboard", "", "Admin", false)]
    [InlineData(typeof(EmployeeController), "UpdateEmployee", "PUT", "{id:int}", "", "Admin,HR", false)]
    [InlineData(typeof(EmployeeController), "DeleteEmployee", "DELETE", "{id:int}", "", "Admin", false)]
    [InlineData(typeof(LeaveRequestController), "GetAllLeaveRequests", "GET", null, "Admin,HR,Employee", null, false)]
    [InlineData(typeof(LeaveRequestController), "GetLeaveRequest", "GET", "{id:int}", "Admin,HR,Employee", null, false)]
    [InlineData(typeof(LeaveRequestController), "ApplyLeave", "POST", null, "Admin,HR,Employee", null, false)]
    [InlineData(typeof(LeaveRequestController), "ApproveLeave", "PUT", "approve/{id:int}", "Admin,HR,Employee", "Admin,HR", false)]
    [InlineData(typeof(LeaveRequestController), "RejectLeave", "PUT", "reject/{id:int}", "Admin,HR,Employee", "Admin,HR", false)]
    [InlineData(typeof(LeaveRequestController), "GetBalances", "GET", "balances", "Admin,HR,Employee", null, false)]
    [InlineData(typeof(LeaveTypeController), "GetLeaveTypes", "GET", null, "", null, false)]
    [InlineData(typeof(LeaveTypeController), "CreateLeaveType", "POST", null, "", "Admin,HR", false)]
    [InlineData(typeof(LeaveTypeController), "UpdateLeaveType", "PUT", "{id:int}", "", "Admin,HR", false)]
    public void RoutesAndRolePoliciesRemainUnchanged(Type controller, string action, string verb, string? template, string? controllerRoles, string? actionRoles, bool anonymous)
    {
        Assert.Equal("api/[controller]", controller.GetCustomAttribute<RouteAttribute>()!.Template);
        Assert.NotNull(controller.GetCustomAttribute<ApiControllerAttribute>());
        var method = controller.GetMethod(action)!;
        var http = method.GetCustomAttributes<HttpMethodAttribute>().Single();
        Assert.Equal(verb, Assert.Single(http.HttpMethods));
        Assert.Equal(template, http.Template);
        Assert.Equal(controllerRoles, Roles(controller));
        Assert.Equal(actionRoles, Roles(method));
        Assert.Equal(anonymous, method.GetCustomAttribute<AllowAnonymousAttribute>() is not null);
    }

    private static string? Roles(MemberInfo member)
    {
        var attribute = member.GetCustomAttribute<AuthorizeAttribute>();
        return attribute is null ? null : attribute.Roles ?? "";
    }
}
