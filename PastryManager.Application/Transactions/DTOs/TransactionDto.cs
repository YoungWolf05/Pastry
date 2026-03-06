using PastryManager.Domain.Enums;

namespace PastryManager.Application.Transactions.DTOs;

public record TransactionDto(
    Guid Id,
    Guid? FromAccountId,
    Guid? ToAccountId,
    decimal Amount,
    string Currency,
    TransactionType TransactionType,
    TransactionStatus Status,
    string? Description,
    decimal? RiskScore,
    decimal? Fee,
    DateTime CreatedAt,
    DateTime? CompletedAt
);
