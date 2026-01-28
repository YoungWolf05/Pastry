using MediatR;
using PastryManager.Application.Common.Models;
using PastryManager.Application.TaskRequests.DTOs;

namespace PastryManager.Application.TaskRequests.Queries.GetTasksByAssignedUser;

public record GetTasksByAssignedUserQuery(Guid UserId) : IRequest<Result<IEnumerable<TaskRequestDto>>>;
