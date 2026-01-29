using MediatR;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using PastryManager.Application.Common.Models;

namespace PastryManager.Application.Common.Behaviors;

public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation("Handling {RequestName}", requestName);

        try
        {
            var response = await next();
            
            stopwatch.Stop();
            
            // Check if response is a failed Result (validation or business logic failure)
            if (response is IResult result && !result.IsSuccess)
            {
                _logger.LogWarning("Request {RequestName} failed after {ElapsedMilliseconds}ms: {Error}", 
                    requestName, stopwatch.ElapsedMilliseconds, result.Error);
            }
            else
            {
                _logger.LogInformation("Handled {RequestName} in {ElapsedMilliseconds}ms", 
                    requestName, stopwatch.ElapsedMilliseconds);
            }

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error handling {RequestName} after {ElapsedMilliseconds}ms", 
                requestName, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}
