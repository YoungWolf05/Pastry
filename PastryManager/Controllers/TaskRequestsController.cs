using MediatR;
using Microsoft.AspNetCore.Mvc;
using PastryManager.Application.TaskRequests.Commands.CreateTaskRequest;
using PastryManager.Application.TaskRequests.Commands.UpdateTaskStatus;
using PastryManager.Application.TaskRequests.DTOs;
using PastryManager.Application.TaskRequests.Queries.GetTasksByAssignedUser;
using PastryManager.Domain.Entities;
using TaskStatus = PastryManager.Domain.Entities.TaskStatus;

namespace PastryManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TaskRequestsController : ControllerBase
{
    private readonly IMediator _mediator;

    public TaskRequestsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Create a new task request
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(TaskRequestDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateTaskRequestDto dto, [FromHeader(Name = "X-User-Id")] Guid createdByUserId)
    {
        var command = new CreateTaskRequestCommand(
            dto.Title,
            dto.Description,
            dto.Priority,
            createdByUserId,
            dto.AssignedToUserId,
            dto.DueDate
        );

        var result = await _mediator.Send(command);

        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Error, errors = result.Errors });
        }

        return CreatedAtAction(nameof(GetByAssignedUser), new { userId = dto.AssignedToUserId }, result.Data);
    }

    /// <summary>
    /// Get tasks assigned to a specific user
    /// </summary>
    [HttpGet("assigned/{userId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<TaskRequestDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByAssignedUser(Guid userId)
    {
        var query = new GetTasksByAssignedUserQuery(userId);
        var result = await _mediator.Send(query);

        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Update task status
    /// </summary>
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(typeof(TaskRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateTaskStatusDto dto)
    {
        var command = new UpdateTaskStatusCommand(id, dto.Status);
        var result = await _mediator.Send(command);

        if (!result.IsSuccess)
        {
            return NotFound(new { error = result.Error });
        }

        return Ok(result.Data);
    }
}

public record UpdateTaskStatusDto(TaskStatus Status);
