using MediatR;
using PastryManager.Application.Common.Models;
using PastryManager.Application.TaskRequests.DTOs;
using PastryManager.Domain.Entities;
using TaskStatus = PastryManager.Domain.Entities.TaskStatus;

namespace PastryManager.Application.TaskRequests.Commands.UpdateTaskStatus;

public record UpdateTaskStatusCommand(
    Guid TaskRequestId,
    TaskStatus Status
) : IRequest<Result<TaskRequestDto>>;
