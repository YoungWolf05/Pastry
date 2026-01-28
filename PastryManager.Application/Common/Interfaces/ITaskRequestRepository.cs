using PastryManager.Domain.Entities;

namespace PastryManager.Application.Common.Interfaces;

public interface ITaskRequestRepository
{
    Task<TaskRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<TaskRequest>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<TaskRequest>> GetByAssignedUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IEnumerable<TaskRequest>> GetByCreatedUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<TaskRequest> AddAsync(TaskRequest taskRequest, CancellationToken cancellationToken = default);
    Task UpdateAsync(TaskRequest taskRequest, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
