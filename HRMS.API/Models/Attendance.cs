namespace HRMS.API.Models
{
    public class Attendance
    {
        public int AttendanceId { get; set; }

        public int EmployeeId { get; set; }

        public DateTime AttendanceDate { get; set; }

        public DateTime? CheckInTime { get; set; }

        public DateTime? CheckOutTime { get; set; }

        public string Status { get; set; } = "Present";

        public Employee? Employee { get; set; }
    }
}