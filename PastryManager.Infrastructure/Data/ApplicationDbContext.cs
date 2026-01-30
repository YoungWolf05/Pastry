using Microsoft.EntityFrameworkCore;
using PastryManager.Application.Common.Interfaces;
using PastryManager.Domain.Entities;
using System.Reflection;

namespace PastryManager.Infrastructure.Data;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<TaskRequest> TaskRequests => Set<TaskRequest>();
    public DbSet<TaskComment> TaskComments => Set<TaskComment>();
    public DbSet<FileAttachment> FileAttachments => Set<FileAttachment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var entries = ChangeTracker
            .Entries()
            .Where(e => e.Entity is BaseEntity && 
                       (e.State == EntityState.Modified));

        foreach (var entry in entries)
        {
            ((BaseEntity)entry.Entity).UpdatedAt = DateTime.UtcNow;
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
