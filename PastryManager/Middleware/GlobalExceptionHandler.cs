using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace PastryManager.Api.Middleware;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is ValidationException validationException)
        {
            _logger.LogWarning("Validation failed: {Message}", validationException.Message);

            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            httpContext.Response.ContentType = "application/json";

            var response = new
            {
                status = StatusCodes.Status400BadRequest,
                title = "Validation Error",
                detail = validationException.Message
            };

            await httpContext.Response.WriteAsJsonAsync(response, cancellationToken);
            return true;
        }

        _logger.LogError(exception, "Unhandled exception occurred");
        
        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        httpContext.Response.ContentType = "application/json";

        var errorResponse = new
        {
            status = StatusCodes.Status500InternalServerError,
            title = "Internal Server Error",
            detail = httpContext.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment() 
                ? exception.Message 
                : "An error occurred while processing your request."
        };

        await httpContext.Response.WriteAsJsonAsync(errorResponse, cancellationToken);
        return true;
    }
}
