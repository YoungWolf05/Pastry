using MediatR;
using PastryManager.Application.Common.Interfaces;
using PastryManager.Application.Common.Models;
using PastryManager.Application.TaskRequests.DTOs;

namespace PastryManager.Application.TaskRequests.Queries.GetTasksByAssignedUser;

public class GetTasksByAssignedUserQueryHandler : IRequestHandler<GetTasksByAssignedUserQuery, Result<IEnumerable<TaskRequestDto>>>
{
    private readonly ITaskRequestRepository _taskRequestRepository;
    private readonly IUserRepository _userRepository;

    public GetTasksByAssignedUserQueryHandler(
        ITaskRequestRepository taskRequestRepository,
        IUserRepository userRepository)
    {
        _taskRequestRepository = taskRequestRepository;
        _userRepository = userRepository;
    }

    public async Task<Result<IEnumerable<TaskRequestDto>>> Handle(GetTasksByAssignedUserQuery request, CancellationToken cancellationToken)
    {
        var tasks = await _taskRequestRepository.GetByAssignedUserIdAsync(request.UserId, cancellationToken);

        var taskDtos = new List<TaskRequestDto>();
        
        foreach (var task in tasks)
        {
            var creator = await _userRepository.GetByIdAsync(task.CreatedByUserId, cancellationToken);
            var assignedUser = await _userRepository.GetByIdAsync(task.AssignedToUserId, cancellationToken);

            taskDtos.Add(new TaskRequestDto(
                task.Id,
                task.Title,
                task.Description,
                task.Priority,
                task.Status,
                task.DueDate,
                task.CompletedAt,
                task.CreatedByUserId,
                $"{creator?.FirstName} {creator?.LastName}",
                task.AssignedToUserId,
                $"{assignedUser?.FirstName} {assignedUser?.LastName}",
                task.CreatedAt
            ));
        }

        return Result<IEnumerable<TaskRequestDto>>.Success(taskDtos);
    }
}
