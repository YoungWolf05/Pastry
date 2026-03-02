using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using PastryManager.Domain.Entities;
using PastryManager.Infrastructure.Data;
using PastryManager.Infrastructure.Services.Kafka;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PastryManager.Infrastructure.Services.Audit;

public interface IAuditService
{
    Task LogActionAsync(string userId, string action, string entityType, string entityId, 
        string? oldValue, string? newValue, string ipAddress, string? userAgent, 
        Dictionary<string, object>? metadata = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Immutable audit logging service for compliance
/// Implements tamper-proof logging with cryptographic hashing
/// </summary>
public class AuditService : IAuditService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AuditService> _logger;
    private readonly IKafkaProducer _kafkaProducer;
    private readonly string _auditLogTopic;

    public AuditService(
        ApplicationDbContext context,
        ILogger<AuditService> logger,
        IKafkaProducer kafkaProducer,
        IOptions<KafkaSettings> kafkaSettings)
    {
        _context = context;
        _logger = logger;
        _kafkaProducer = kafkaProducer;
        _auditLogTopic = kafkaSettings.Value.AuditLogTopic;
    }

    public async Task LogActionAsync(
        string userId,
        string action,
        string entityType,
        string entityId,
        string? oldValue,
        string? newValue,
        string ipAddress,
        string? userAgent,
        Dictionary<string, object>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var auditLog = new AuditLog
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                UserId = userId,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                OldValue = oldValue,
                NewValue = newValue,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                Metadata = metadata != null ? JsonSerializer.Serialize(metadata) : null,
                Hash = string.Empty // Will be computed below
            };

            // Compute tamper-proof hash
            var hash = ComputeAuditHash(auditLog);
            auditLog.Hash = hash;

            // Store in database (append-only)
            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync(cancellationToken);

            // Publish to Kafka for real-time monitoring
            await _kafkaProducer.ProduceAsync(_auditLogTopic, auditLog.Id.ToString(), auditLog, cancellationToken);

            _logger.LogInformation(
                "Audit log created: User {UserId} performed {Action} on {EntityType} {EntityId}",
                userId, action, entityType, entityId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create audit log");
            // Don't throw - audit logging should not break business operations
        }
    }

    private static string ComputeAuditHash(AuditLog auditLog)
    {
        // Create hash from all fields except the hash itself
        var data = $"{auditLog.Id}|{auditLog.Timestamp:O}|{auditLog.UserId}|{auditLog.Action}|" +
                   $"{auditLog.EntityType}|{auditLog.EntityId}|{auditLog.OldValue}|{auditLog.NewValue}|" +
                   $"{auditLog.IpAddress}|{auditLog.UserAgent}|{auditLog.Metadata}";

        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(data));
        return Convert.ToBase64String(hashBytes);
    }
}
