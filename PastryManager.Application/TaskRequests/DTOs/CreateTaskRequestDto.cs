using PastryManager.Domain.Entities;

namespace PastryManager.Application.TaskRequests.DTOs;

public record CreateTaskRequestDto(
    string Title,
    string Description,
    TaskPriority Priority,
    Guid AssignedToUserId,
    DateTime? DueDate
);
