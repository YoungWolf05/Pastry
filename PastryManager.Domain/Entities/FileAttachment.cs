using PastryManager.Domain.Enums;

namespace PastryManager.Domain.Entities;

public class FileAttachment : BaseEntity
{
    public string FileName { get; set; } = string.Empty;
    public string S3Key { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    
    // Entity association
    public EntityType EntityType { get; set; }
    public Guid EntityId { get; set; }
    
    // Audit
    public Guid UploadedBy { get; set; }
    public User? UploadedByUser { get; set; }
}
