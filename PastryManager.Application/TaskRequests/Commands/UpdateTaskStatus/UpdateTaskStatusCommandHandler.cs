using MediatR;
using PastryManager.Application.Common.Interfaces;
using PastryManager.Application.Common.Models;
using PastryManager.Application.TaskRequests.DTOs;
using PastryManager.Domain.Entities;
using TaskStatus = PastryManager.Domain.Entities.TaskStatus;

namespace PastryManager.Application.TaskRequests.Commands.UpdateTaskStatus;

public class UpdateTaskStatusCommandHandler : IRequestHandler<UpdateTaskStatusCommand, Result<TaskRequestDto>>
{
    private readonly ITaskRequestRepository _taskRequestRepository;
    private readonly IUserRepository _userRepository;

    public UpdateTaskStatusCommandHandler(
        ITaskRequestRepository taskRequestRepository,
        IUserRepository userRepository)
    {
        _taskRequestRepository = taskRequestRepository;
        _userRepository = userRepository;
    }

    public async Task<Result<TaskRequestDto>> Handle(UpdateTaskStatusCommand request, CancellationToken cancellationToken)
    {
        var taskRequest = await _taskRequestRepository.GetByIdAsync(request.TaskRequestId, cancellationToken);
        
        if (taskRequest == null)
        {
            return Result<TaskRequestDto>.Failure("Task request not found");
        }

        taskRequest.Status = request.Status;
        taskRequest.UpdatedAt = DateTime.UtcNow;

        if (request.Status == TaskStatus.Completed)
        {
            taskRequest.CompletedAt = DateTime.UtcNow;
        }

        await _taskRequestRepository.UpdateAsync(taskRequest, cancellationToken);

        // Get user information
        var creator = await _userRepository.GetByIdAsync(taskRequest.CreatedByUserId, cancellationToken);
        var assignedUser = await _userRepository.GetByIdAsync(taskRequest.AssignedToUserId, cancellationToken);

        var taskDto = new TaskRequestDto(
            taskRequest.Id,
            taskRequest.Title,
            taskRequest.Description,
            taskRequest.Priority,
            taskRequest.Status,
            taskRequest.DueDate,
            taskRequest.CompletedAt,
            taskRequest.CreatedByUserId,
            $"{creator?.FirstName} {creator?.LastName}",
            taskRequest.AssignedToUserId,
            $"{assignedUser?.FirstName} {assignedUser?.LastName}",
            taskRequest.CreatedAt
        );

        return Result<TaskRequestDto>.Success(taskDto);
    }
}
