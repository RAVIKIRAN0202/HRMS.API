namespace HRMS.API.Models
{
    public class Employee
    {
        public int EmployeeId { get; set; }

        public int UserId { get; set; }

        public string EmployeeCode { get; set; } = string.Empty;

        public int DepartmentId { get; set; }

        public int DesignationId { get; set; }

        public DateTime DateOfJoining { get; set; }

        public string? Phone { get; set; }

        public string? Address { get; set; }

        public decimal Salary { get; set; }

        public string Status { get; set; } = "Active";

        public DateTime CreatedAt { get; set; }

        public User? User { get; set; }

        public Department? Department { get; set; }

        public Designation? Designation { get; set; }
    }
}