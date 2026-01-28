using Microsoft.EntityFrameworkCore;
using PastryManager.Application.Common.Interfaces;
using PastryManager.Domain.Entities;
using PastryManager.Infrastructure.Data;

namespace PastryManager.Infrastructure.Repositories;

public class TaskRequestRepository : ITaskRequestRepository
{
    private readonly ApplicationDbContext _context;

    public TaskRequestRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<TaskRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.TaskRequests
            .Include(t => t.CreatedByUser)
            .Include(t => t.AssignedToUser)
            .Include(t => t.Comments)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<TaskRequest>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.TaskRequests
            .Include(t => t.CreatedByUser)
            .Include(t => t.AssignedToUser)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<TaskRequest>> GetByAssignedUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.TaskRequests
            .Include(t => t.CreatedByUser)
            .Include(t => t.AssignedToUser)
            .Where(t => t.AssignedToUserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<TaskRequest>> GetByCreatedUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.TaskRequests
            .Include(t => t.CreatedByUser)
            .Include(t => t.AssignedToUser)
            .Where(t => t.CreatedByUserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<TaskRequest> AddAsync(TaskRequest taskRequest, CancellationToken cancellationToken = default)
    {
        await _context.TaskRequests.AddAsync(taskRequest, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        
        // Reload with navigation properties
        return (await GetByIdAsync(taskRequest.Id, cancellationToken))!;
    }

    public async Task UpdateAsync(TaskRequest taskRequest, CancellationToken cancellationToken = default)
    {
        _context.TaskRequests.Update(taskRequest);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var taskRequest = await GetByIdAsync(id, cancellationToken);
        if (taskRequest != null)
        {
            taskRequest.IsDeleted = true;
            await UpdateAsync(taskRequest, cancellationToken);
        }
    }
}
