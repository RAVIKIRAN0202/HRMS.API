namespace HRMS.API.Models
{
    public class User
    {
        public int UserId { get; set; }

        public string FullName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string PasswordHash { get; set; } = string.Empty;

        public int RoleId { get; set; }

        public bool IsActive { get; set; }
        public bool MustChangePassword { get; set; }
        public int SessionVersion { get; set; }

        public DateTime CreatedAt { get; set; }

        // Navigation Property
        public Role? Role { get; set; }
    }
}
