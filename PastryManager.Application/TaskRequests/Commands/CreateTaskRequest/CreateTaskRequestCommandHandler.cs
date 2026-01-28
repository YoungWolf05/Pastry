using MediatR;
using PastryManager.Application.Common.Interfaces;
using PastryManager.Application.Common.Models;
using PastryManager.Application.TaskRequests.DTOs;
using PastryManager.Domain.Entities;
using TaskStatus = PastryManager.Domain.Entities.TaskStatus;

namespace PastryManager.Application.TaskRequests.Commands.CreateTaskRequest;

public class CreateTaskRequestCommandHandler : IRequestHandler<CreateTaskRequestCommand, Result<TaskRequestDto>>
{
    private readonly ITaskRequestRepository _taskRequestRepository;
    private readonly IUserRepository _userRepository;

    public CreateTaskRequestCommandHandler(
        ITaskRequestRepository taskRequestRepository,
        IUserRepository userRepository)
    {
        _taskRequestRepository = taskRequestRepository;
        _userRepository = userRepository;
    }

    public async Task<Result<TaskRequestDto>> Handle(CreateTaskRequestCommand request, CancellationToken cancellationToken)
    {
        // Verify creator exists
        var creator = await _userRepository.GetByIdAsync(request.CreatedByUserId, cancellationToken);
        if (creator == null)
        {
            return Result<TaskRequestDto>.Failure("Creator user not found");
        }

        // Verify assigned user exists
        var assignedUser = await _userRepository.GetByIdAsync(request.AssignedToUserId, cancellationToken);
        if (assignedUser == null)
        {
            return Result<TaskRequestDto>.Failure("Assigned user not found");
        }

        // Create task request
        var taskRequest = new TaskRequest
        {
            Title = request.Title,
            Description = request.Description,
            Priority = request.Priority,
            Status = TaskStatus.Pending,
            CreatedByUserId = request.CreatedByUserId,
            AssignedToUserId = request.AssignedToUserId,
            DueDate = request.DueDate
        };

        var createdTask = await _taskRequestRepository.AddAsync(taskRequest, cancellationToken);

        var taskDto = new TaskRequestDto(
            createdTask.Id,
            createdTask.Title,
            createdTask.Description,
            createdTask.Priority,
            createdTask.Status,
            createdTask.DueDate,
            createdTask.CompletedAt,
            createdTask.CreatedByUserId,
            $"{creator.FirstName} {creator.LastName}",
            createdTask.AssignedToUserId,
            $"{assignedUser.FirstName} {assignedUser.LastName}",
            createdTask.CreatedAt
        );

        return Result<TaskRequestDto>.Success(taskDto);
    }
}
