namespace HRMS.API.Models
{
    public class Role
    {
        public int RoleId { get; set; }

        public string RoleName { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }
    }
}