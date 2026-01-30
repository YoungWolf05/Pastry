# Pastry Manager MCP Server

A Model Context Protocol (MCP) server for the Pastry Manager backend, exposing task management and user operations as MCP tools and resources.

## Overview

This MCP server integrates with the existing Pastry Manager Clean Architecture backend, providing AI assistants (like Claude) with structured access to:

- **User Management**: Register users, retrieve user details
- **Task Management**: Create tasks, update task status, query assigned tasks
- **CQRS Integration**: All operations use existing MediatR handlers from the Application layer

## Architecture

The MCP server runs **alongside** the REST API, sharing the same Application and Infrastructure layers:

```
┌─────────────────────────────────────────┐
│         Pastry Manager System           │
└────────────┬────────────────────────────┘
             │
       ┌─────┴──────┐
       │            │
┌──────▼──────┐ ┌──▼──────────────────┐
│  REST API   │ │   MCP Server        │
│  (Port 8080)│ │   (stdio/SSE)       │
└──────┬──────┘ └──┬──────────────────┘
       │            │
       └─────┬──────┘
             │
      ┌──────▼────────┐
      │  Application  │
      │  (CQRS)       │
      │  - Commands   │
      │  - Queries    │
      └──────┬────────┘
             │
      ┌──────▼───────────┐
      │  Infrastructure  │
      │  - Repositories  │
      │  - EF Core       │
      │  - PostgreSQL    │
      └──────────────────┘
```

## MCP Tools (Write Operations)

### 1. `register_user`
Register a new user in the system.

**Parameters:**
- `email` (string, required): User email address
- `firstName` (string, required): User first name
- `lastName` (string, required): User last name
- `phoneNumber` (string, optional): User phone number
- `password` (string, required): User password (min 8 characters, must contain uppercase, lowercase, number, special char)

**Example:**
```json
{
  "email": "john.doe@example.com",
  "firstName": "John",
  "lastName": "Doe",
  "phoneNumber": "+1234567890",
  "password": "SecurePass123!"
}
```

### 2. `create_task`
Create a new task request.

**Parameters:**
- `title` (string, required): Task title
- `description` (string, required): Task description
- `priority` (string, required): Task priority - "Low", "Medium", "High", or "Critical"
- `createdByUserId` (string, required): GUID of user creating the task
- `assignedToUserId` (string, required): GUID of user assigned to the task
- `dueDate` (string, optional): Due date in ISO 8601 format

**Example:**
```json
{
  "title": "Fix login bug",
  "description": "Users unable to login with special characters in password",
  "priority": "High",
  "createdByUserId": "12345678-1234-1234-1234-123456789012",
  "assignedToUserId": "87654321-4321-4321-4321-210987654321",
  "dueDate": "2026-02-15T17:00:00Z"
}
```

### 3. `update_task_status`
Update the status of an existing task.

**Parameters:**
- `taskId` (string, required): GUID of the task to update
- `status` (string, required): New status - "Pending", "InProgress", "Completed", "Cancelled", or "OnHold"

**Example:**
```json
{
  "taskId": "12345678-1234-1234-1234-123456789012",
  "status": "InProgress"
}
```

## MCP Resources (Read Operations)

### 1. `user://{userId}`
Retrieve user details by ID.

**URI Format:** `user://12345678-1234-1234-1234-123456789012`

**Returns:** JSON object with user details (id, email, firstName, lastName, phoneNumber, role, isActive)

### 2. `tasks://assigned/{userId}`
Get all tasks assigned to a specific user.

**URI Format:** `tasks://assigned/12345678-1234-1234-1234-123456789012`

**Returns:** JSON array of task objects with details (id, title, description, priority, status, dueDate, assignedUser, createdByUser)

## Installation & Setup

### Prerequisites
- .NET 8.0 SDK
- PostgreSQL database running
- Existing Pastry Manager backend configured

### 1. Build the MCP Server

```powershell
cd PastryManager.MCP
dotnet restore
dotnet build
```

### 2. Configure Database Connection

Edit `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=pastrymanager;Username=postgres;Password=YOUR_PASSWORD"
  }
}
```

### 3. Run the MCP Server

```powershell
dotnet run --project PastryManager.MCP
```

The server will start and listen on stdio for MCP requests.

## Integration with Claude Desktop

### Configuration

Add to your Claude Desktop configuration (`%APPDATA%\Claude\claude_desktop_config.json` on Windows):

```json
{
  "mcpServers": {
    "pastry-manager": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "C:\\Users\\SNGCF\\source\\repos\\Pastry\\PastryManager.MCP\\PastryManager.MCP.csproj"
      ],
      "env": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}
```

### Testing with Claude

Once configured, you can ask Claude:

- "Register a new user with email test@example.com, first name Test, last name User, and password SecurePass123!"
- "Create a high priority task titled 'Database Optimization' assigned to user {userId}"
- "Get details for user {userId}"
- "Show me all tasks assigned to user {userId}"
- "Update task {taskId} status to InProgress"

## Integration with Cline (VS Code Extension)

Add to Cline MCP settings:

```json
{
  "pastry-manager": {
    "command": "dotnet",
    "args": ["run", "--project", "PastryManager.MCP"],
    "cwd": "C:\\Users\\SNGCF\\source\\repos\\Pastry"
  }
}
```

## Development

### Project Structure

```
PastryManager.MCP/
├── Program.cs                      # Entry point
├── PastryManager.MCP.csproj       # Project file with dependencies
├── appsettings.json               # Configuration
├── appsettings.Development.json   # Development config
└── McpServer/
    ├── IPastryMcpServer.cs        # Server interface
    ├── PastryMcpServer.cs         # Main MCP server implementation
    └── DependencyInjection.cs     # DI registration
```

### Dependencies

- **ModelContextProtocol** (0.7.0-preview.1): Official Microsoft MCP SDK
- **ModelContextProtocol.Core** (0.7.0-preview.1): Core MCP protocol
- **MediatR** (13.0.0): CQRS pattern for handlers
- **Microsoft.Extensions.Hosting**: Generic host for DI and lifecycle
- **Project References**: Application, Infrastructure, Domain layers

### Adding New Tools

To add a new MCP tool:

1. Create the command/query in the Application layer (if not exists)
2. Register the tool in `PastryMcpServer.RegisterTools()`:

```csharp
_mcpServer.AddTool(
    "tool_name",
    "Tool description",
    new { /* JSON schema for parameters */ },
    async (arguments) =>
    {
        // Parse arguments
        // Call mediator.Send(command/query)
        // Return ToolResult
    });
```

### Adding New Resources

To add a new MCP resource:

1. Create the query in the Application layer (if not exists)
2. Register the resource in `PastryMcpServer.RegisterResources()`:

```csharp
_mcpServer.AddResource(
    "resource://uri/{parameter}",
    "Resource description",
    "application/json",
    async (uri) =>
    {
        // Parse URI
        // Call mediator.Send(query)
        // Return ResourceContents
    });
```

## Logging

The server logs to console with structured logging:

- **Information**: Server startup, tool/resource invocations
- **Debug**: Detailed request/response data (Development only)
- **Error**: Exceptions and failures

View logs in the terminal where the server is running.

## Error Handling

All MCP tools and resources include error handling:

- **Validation Errors**: Returned from FluentValidation via Result pattern
- **Not Found**: When entities don't exist
- **Exceptions**: Caught and returned as MCP error responses

Errors are logged and returned to the client with descriptive messages.

## Security Considerations

⚠️ **Important**: This MCP server currently runs with **no authentication**. It assumes:

- The MCP client (Claude/Cline) is trusted
- The server runs locally on the developer's machine
- Database credentials are protected via connection strings

**For production use**, consider:
- Adding MCP-level authentication/authorization
- Restricting tool/resource access based on user context
- Using connection pooling and rate limiting
- Running behind a secure proxy

## Testing

### Manual Testing with MCP Inspector

Use the official [MCP Inspector](https://github.com/modelcontextprotocol/inspector):

```powershell
npx @modelcontextprotocol/inspector dotnet run --project PastryManager.MCP
```

This provides a web UI to test tools and resources interactively.

### Integration Testing

Create tests that:
1. Start the MCP server
2. Send MCP protocol messages via stdio
3. Verify tool/resource responses
4. Check database state changes

## Troubleshooting

### Server won't start
- Check PostgreSQL is running: `Test-NetConnection localhost -Port 5432`
- Verify connection string in `appsettings.json`
- Ensure database migrations ran: `dotnet ef database update --project PastryManager.Infrastructure`

### Tools returning errors
- Check logs for detailed error messages
- Verify user/task GUIDs exist in database
- Validate input parameters match JSON schema

### Claude Desktop not seeing server
- Restart Claude Desktop after config changes
- Verify absolute path to `.csproj` file
- Check `%APPDATA%\Claude\logs` for MCP connection errors

## Roadmap

Future enhancements:

- [ ] Add more tools: `assign_task`, `add_task_comment`, `list_all_users`
- [ ] Add more resources: `tasks://created/{userId}`, `stats://dashboard`
- [ ] Implement MCP prompts for common workflows
- [ ] Add SSE transport for web-based clients
- [ ] Support pagination for large result sets
- [ ] Add authentication/authorization layer
- [ ] Performance optimization with caching

## License

Same license as the Pastry Manager project.

## Contributing

Follow the main project's contribution guidelines. When adding MCP features:

1. Keep business logic in the Application layer
2. MCP server should only handle protocol concerns
3. Reuse existing CQRS handlers whenever possible
4. Add comprehensive logging for diagnostics
5. Update this README with new tools/resources
