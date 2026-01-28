namespace PastryManager.Domain.Entities;

public class TaskRequest : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;
    public TaskStatus Status { get; set; } = TaskStatus.Pending;
    public DateTime? DueDate { get; set; }
    public DateTime? CompletedAt { get; set; }
    
    // Foreign keys
    public Guid CreatedByUserId { get; set; }
    public Guid AssignedToUserId { get; set; }
    
    // Navigation properties
    public User CreatedByUser { get; set; } = null!;
    public User AssignedToUser { get; set; } = null!;
    public ICollection<TaskComment> Comments { get; set; } = new List<TaskComment>();
}

public enum TaskPriority
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}

public enum TaskStatus
{
    Pending = 0,
    InProgress = 1,
    Completed = 2,
    Cancelled = 3,
    OnHold = 4
}
