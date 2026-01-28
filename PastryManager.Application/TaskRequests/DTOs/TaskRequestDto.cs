using PastryManager.Domain.Entities;
using TaskStatus = PastryManager.Domain.Entities.TaskStatus;

namespace PastryManager.Application.TaskRequests.DTOs;

public record TaskRequestDto(
    Guid Id,
    string Title,
    string Description,
    TaskPriority Priority,
    TaskStatus Status,
    DateTime? DueDate,
    DateTime? CompletedAt,
    Guid CreatedByUserId,
    string CreatedByUserName,
    Guid AssignedToUserId,
    string AssignedToUserName,
    DateTime CreatedAt
);
