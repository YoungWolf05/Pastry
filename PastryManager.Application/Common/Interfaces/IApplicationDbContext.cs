using Microsoft.EntityFrameworkCore;
using PastryManager.Domain.Entities;

namespace PastryManager.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<TaskRequest> TaskRequests { get; }
    DbSet<TaskComment> TaskComments { get; }
    DbSet<FileAttachment> FileAttachments { get; }
    
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
