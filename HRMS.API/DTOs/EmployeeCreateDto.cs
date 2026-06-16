namespace HRMS.API.DTOs
{
    public class EmployeeCreateDto
    {
        public int UserId { get; set; }
        public string EmployeeCode { get; set; } = string.Empty;
        public int DepartmentId { get; set; }
        public int DesignationId { get; set; }
        public DateTime DateOfJoining { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public decimal Salary { get; set; }
        public string Status { get; set; } = "Active";
    }
}