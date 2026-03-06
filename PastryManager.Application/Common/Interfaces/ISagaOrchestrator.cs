namespace PastryManager.Application.Common.Interfaces;

public interface ISagaOrchestrator
{
    Task<(bool Success, Guid? TransactionId, string? ErrorMessage)> ExecuteTransferSagaAsync(
        Guid fromAccountId,
        Guid toAccountId,
        decimal amount,
        string currency,
        string idempotencyKey,
        CancellationToken cancellationToken = default);
}
