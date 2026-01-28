namespace PastryManager.Domain.Entities;

public class User : BaseEntity
{
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.User;
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }
    
    // Navigation properties
    public ICollection<TaskRequest> AssignedTasks { get; set; } = new List<TaskRequest>();
    public ICollection<TaskRequest> CreatedTasks { get; set; } = new List<TaskRequest>();
}

public enum UserRole
{
    User = 0,
    Manager = 1,
    Admin = 2
}
