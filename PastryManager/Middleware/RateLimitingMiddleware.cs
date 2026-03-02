using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace PastryManager.Middleware.Security;

/// <summary>
/// Rate limiting middleware to prevent brute force and DDoS attacks
/// Implements sliding window rate limiting per IP address
/// </summary>
public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private static readonly ConcurrentDictionary<string, Queue<DateTime>> _requestTimestamps = new();
    private readonly int _maxRequestsPerMinute = 100;
    private readonly TimeSpan _timeWindow = TimeSpan.FromMinutes(1);

    public RateLimitingMiddleware(RequestDelegate next, ILogger<RateLimitingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        
        // Clean up old entries periodically
        CleanupOldEntries();

        var timestamps = _requestTimestamps.GetOrAdd(clientIp, _ => new Queue<DateTime>());

        lock (timestamps)
        {
            // Remove timestamps outside the time window
            while (timestamps.Count > 0 && DateTime.UtcNow - timestamps.Peek() > _timeWindow)
            {
                timestamps.Dequeue();
            }

            if (timestamps.Count >= _maxRequestsPerMinute)
            {
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.Response.Headers["Retry-After"] = "60";
                
                _logger.LogWarning("Rate limit exceeded for IP: {ClientIp}", clientIp);
                context.Response.WriteAsync("Rate limit exceeded. Please try again later.").Wait();
                return;
            }

            timestamps.Enqueue(DateTime.UtcNow);
        }

        await _next(context);
    }

    private static void CleanupOldEntries()
    {
        foreach (var kvp in _requestTimestamps)
        {
            lock (kvp.Value)
            {
                if (kvp.Value.Count == 0 || DateTime.UtcNow - kvp.Value.Peek() > TimeSpan.FromMinutes(5))
                {
                    _requestTimestamps.TryRemove(kvp.Key, out _);
                }
            }
        }
    }
}
