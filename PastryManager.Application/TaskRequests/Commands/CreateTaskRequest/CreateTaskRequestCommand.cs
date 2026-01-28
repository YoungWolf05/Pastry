using MediatR;
using PastryManager.Application.Common.Models;
using PastryManager.Application.TaskRequests.DTOs;
using PastryManager.Domain.Entities;

namespace PastryManager.Application.TaskRequests.Commands.CreateTaskRequest;

public record CreateTaskRequestCommand(
    string Title,
    string Description,
    TaskPriority Priority,
    Guid CreatedByUserId,
    Guid AssignedToUserId,
    DateTime? DueDate
) : IRequest<Result<TaskRequestDto>>;
