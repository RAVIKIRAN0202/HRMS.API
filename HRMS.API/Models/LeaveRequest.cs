namespace HRMS.API.Models
{
    public class LeaveRequest
    {
        public int LeaveRequestId { get; set; }

        public int EmployeeId { get; set; }
        public int LeaveTypeId { get; set; }

        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }

        public string? Reason { get; set; }
        public string Status { get; set; } = "Pending";

        public int? ApprovedBy { get; set; }

        public DateTime CreatedAt { get; set; }

        public Employee? Employee { get; set; }
        public LeaveType? LeaveType { get; set; }
    }
}