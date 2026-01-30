using MediatR;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using PastryManager.Application.TaskRequests.Commands.CreateTaskRequest;
using PastryManager.Application.TaskRequests.Commands.UpdateTaskStatus;
using PastryManager.Application.TaskRequests.Queries.GetTasksByAssignedUser;
using PastryManager.Application.Users.Commands.RegisterUser;
using PastryManager.Application.Users.Queries.GetUserById;
using PastryManager.Domain.Entities;
using System.ComponentModel;
using System.Text.Json;

namespace PastryManager.MCP.McpServer;

/// <summary>
/// MCP Tools for Pastry Manager - User Management
/// </summary>
[McpServerToolType]
public class UserManagementTools(IMediator mediator, ILogger<UserManagementTools> logger)
{
    [McpServerTool(Name = "register_user"), Description("Register a new user in the system")]
    public async Task<string> RegisterUser(
        [Description("User email address")] string email,
        [Description("User first name")] string firstName,
        [Description("User last name")] string lastName,
        [Description("User password (min 8 characters)")] string password,
        [Description("User phone number (optional)")] string? phoneNumber = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var command = new RegisterUserCommand(
                email,
                firstName,
                lastName,
                phoneNumber,
                password
            );

            var result = await mediator.Send(command, cancellationToken);

            if (result.IsSuccess)
            {
                return JsonSerializer.Serialize(new
                {
                    success = true,
                    user = result.Data
                });
            }

            return JsonSerializer.Serialize(new
            {
                success = false,
                error = result.Error
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing register_user tool");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [McpServerTool(Name = "get_user"), Description("Get user details by ID")]
    public async Task<string> GetUser(
        [Description("User ID (GUID)")] string userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = new GetUserByIdQuery(Guid.Parse(userId));
            var result = await mediator.Send(query, cancellationToken);

            if (result.IsSuccess)
            {
                return JsonSerializer.Serialize(new
                {
                    success = true,
                    user = result.Data
                });
            }

            return JsonSerializer.Serialize(new
            {
                success = false,
                error = result.Error
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing get_user tool");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }
}

/// <summary>
/// MCP Tools for Pastry Manager - Task Management
/// </summary>
[McpServerToolType]
public class TaskManagementTools(IMediator mediator, ILogger<TaskManagementTools> logger)
{
    [McpServerTool(Name = "create_task"), Description("Create a new task request")]
    public async Task<string> CreateTask(
        [Description("Task title")] string title,
        [Description("Task description")] string description,
        [Description("Task priority: Low, Medium, High, or Critical")] string priority,
        [Description("GUID of user creating the task")] string createdByUserId,
        [Description("GUID of user assigned to the task")] string assignedToUserId,
        [Description("Due date (ISO 8601 format, optional)")] string? dueDate = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var command = new CreateTaskRequestCommand(
                title,
                description,
                Enum.Parse<TaskPriority>(priority),
                Guid.Parse(createdByUserId),
                Guid.Parse(assignedToUserId),
                !string.IsNullOrEmpty(dueDate) ? DateTime.Parse(dueDate) : null
            );

            var result = await mediator.Send(command, cancellationToken);

            if (result.IsSuccess)
            {
                return JsonSerializer.Serialize(new
                {
                    success = true,
                    task = result.Data
                });
            }

            return JsonSerializer.Serialize(new
            {
                success = false,
                error = result.Error
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing create_task tool");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [McpServerTool(Name = "update_task_status"), Description("Update the status of an existing task")]
    public async Task<string> UpdateTaskStatus(
        [Description("GUID of the task to update")] string taskId,
        [Description("New task status: Pending, InProgress, Completed, Cancelled, or OnHold")] string status,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var command = new UpdateTaskStatusCommand(
                Guid.Parse(taskId),
                Enum.Parse<Domain.Entities.TaskStatus>(status)
            );

            var result = await mediator.Send(command, cancellationToken);

            if (result.IsSuccess)
            {
                return JsonSerializer.Serialize(new
                {
                    success = true,
                    task = result.Data
                });
            }

            return JsonSerializer.Serialize(new
            {
                success = false,
                error = result.Error
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing update_task_status tool");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [McpServerTool(Name = "get_assigned_tasks"), Description("Get all tasks assigned to a specific user")]
    public async Task<string> GetAssignedTasks(
        [Description("User ID (GUID)")] string userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = new GetTasksByAssignedUserQuery(Guid.Parse(userId));
            var result = await mediator.Send(query, cancellationToken);

            if (result.IsSuccess)
            {
                return JsonSerializer.Serialize(new
                {
                    success = true,
                    tasks = result.Data
                });
            }

            return JsonSerializer.Serialize(new
            {
                success = false,
                error = result.Error
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing get_assigned_tasks tool");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }
}
