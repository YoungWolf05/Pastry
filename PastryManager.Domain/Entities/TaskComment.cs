namespace PastryManager.Domain.Entities;

public class TaskComment : BaseEntity
{
    public string Content { get; set; } = string.Empty;
    
    // Foreign keys
    public Guid TaskRequestId { get; set; }
    public Guid UserId { get; set; }
    
    // Navigation properties
    public TaskRequest TaskRequest { get; set; } = null!;
    public User User { get; set; } = null!;
}
