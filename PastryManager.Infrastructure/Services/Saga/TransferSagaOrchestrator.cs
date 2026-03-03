using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using PastryManager.Domain.Entities;
using PastryManager.Domain.Enums;
using PastryManager.Domain.Events;
using PastryManager.Infrastructure.Data;
using PastryManager.Infrastructure.Services.Kafka;
using PastryManager.Infrastructure.Services.EventSourcing;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace PastryManager.Infrastructure.Services.Saga;

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

/// <summary>
/// Saga orchestrator for distributed transactions using Kafka
/// Implements compensating transactions for rollback scenarios
/// Uses circuit breaker pattern for resilience
/// </summary>
public class TransferSagaOrchestrator : ISagaOrchestrator
{
    private readonly ApplicationDbContext _context;
    private readonly IEventStoreService _eventStore;
    private readonly IKafkaProducer _kafkaProducer;
    private readonly ILogger<TransferSagaOrchestrator> _logger;
    private readonly KafkaSettings _kafkaSettings;
    private readonly ResiliencePipeline _resiliencePipeline;

    public TransferSagaOrchestrator(
        ApplicationDbContext context,
        IEventStoreService eventStore,
        IKafkaProducer kafkaProducer,
        ILogger<TransferSagaOrchestrator> logger,
        IOptions<KafkaSettings> kafkaSettings)
    {
        _context = context;
        _eventStore = eventStore;
        _kafkaProducer = kafkaProducer;
        _logger = logger;
        _kafkaSettings = kafkaSettings.Value;

        // Configure resilience pipeline with circuit breaker and retry
        _resiliencePipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(2),
                BackoffType = DelayBackoffType.Exponential
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                SamplingDuration = TimeSpan.FromSeconds(30),
                MinimumThroughput = 10,
                BreakDuration = TimeSpan.FromSeconds(30)
            })
            .Build();
    }

    public async Task<(bool Success, Guid? TransactionId, string? ErrorMessage)> ExecuteTransferSagaAsync(
        Guid fromAccountId,
        Guid toAccountId,
        decimal amount,
        string currency,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var transactionId = Guid.NewGuid();
        var correlationId = Guid.NewGuid().ToString();

        try
        {
            // Check idempotency - prevent duplicate transactions
            var existingTransaction = await _context.Transactions
                .FirstOrDefaultAsync(t => t.IdempotencyKey == idempotencyKey, cancellationToken);

            if (existingTransaction != null)
            {
                _logger.LogWarning("Duplicate transaction detected with idempotency key: {Key}", idempotencyKey);
                return (existingTransaction.Status == TransactionStatus.Completed, existingTransaction.Id, 
                    existingTransaction.Status == TransactionStatus.Completed ? null : "Transaction already exists but failed");
            }

            // Step 1: Initiate — publish saga started event
            var transaction = await InitiateTransactionAsync(transactionId, fromAccountId, toAccountId, 
                amount, currency, idempotencyKey, cancellationToken);
            await PublishSagaEventAsync("SagaStarted", transactionId, correlationId,
                new { step = "Initiate", fromAccountId, toAccountId, amount, currency }, cancellationToken);

            // Step 2: Debit — publish saga step event
            var debitSuccess = await _resiliencePipeline.ExecuteAsync(async ct =>
                await DebitAccountAsync(fromAccountId, amount, transaction, ct), cancellationToken);

            if (!debitSuccess)
            {
                await CompensateTransactionAsync(transaction, "Debit failed", cancellationToken);
                await PublishSagaEventAsync("SagaFailed", transactionId, correlationId,
                    new { step = "Debit", reason = "Insufficient funds or account unavailable", compensated = true }, cancellationToken);
                return (false, transactionId, "Insufficient funds or account unavailable");
            }
            await PublishSagaEventAsync("AccountDebited", transactionId, correlationId,
                new { step = "Debit", fromAccountId, amount }, cancellationToken);

            // Step 3: Credit — publish saga step event
            var creditSuccess = await _resiliencePipeline.ExecuteAsync(async ct =>
                await CreditAccountAsync(toAccountId, amount, transaction, ct), cancellationToken);

            if (!creditSuccess)
            {
                await CompensateDebitAsync(fromAccountId, amount, transaction, cancellationToken);
                await CompensateTransactionAsync(transaction, "Credit failed", cancellationToken);
                await PublishSagaEventAsync("SagaFailed", transactionId, correlationId,
                    new { step = "Credit", reason = "Failed to credit destination account", compensated = true }, cancellationToken);
                return (false, transactionId, "Failed to credit destination account");
            }
            await PublishSagaEventAsync("AccountCredited", transactionId, correlationId,
                new { step = "Credit", toAccountId, amount }, cancellationToken);

            // Step 4: Complete — publish saga completed event
            await CompleteTransactionAsync(transaction, cancellationToken);
            await PublishSagaEventAsync("SagaCompleted", transactionId, correlationId,
                new { step = "Complete", fromAccountId, toAccountId, amount, currency, status = "Completed" }, cancellationToken);

            _logger.LogInformation(
                "Transfer saga completed successfully. Transaction: {TransactionId}, Amount: {Amount}",
                transactionId, amount);

            return (true, transactionId, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Transfer saga failed for transaction: {TransactionId}", transactionId);
            await PublishSagaEventAsync("SagaFailed", transactionId, correlationId,
                new { reason = ex.Message, compensated = false }, cancellationToken);
            return (false, transactionId, $"Transfer failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Publishes a saga state-change event to the transfer-saga-events topic
    /// </summary>
    private async Task PublishSagaEventAsync(string eventType, Guid transactionId, string correlationId,
        object payload, CancellationToken cancellationToken)
    {
        try
        {
            var sagaEvent = new
            {
                eventType,
                transactionId,
                correlationId,
                occurredAt = DateTime.UtcNow,
                payload
            };
            await _kafkaProducer.ProduceAsync(
                KafkaTopics.TransferSagaEvents,
                transactionId.ToString(),
                sagaEvent,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish saga event {EventType} for {TransactionId}", eventType, transactionId);
        }
    }

    private async Task<Transaction> InitiateTransactionAsync(
        Guid transactionId,
        Guid fromAccountId,
        Guid toAccountId,
        decimal amount,
        string currency,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var transaction = new Transaction
        {
            Id = transactionId,
            FromAccountId = fromAccountId,
            ToAccountId = toAccountId,
            Amount = amount,
            Currency = currency,
            TransactionType = TransactionType.Transfer,
            Status = TransactionStatus.Pending,
            IdempotencyKey = idempotencyKey,
            InitiatedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync(cancellationToken);

        // Publish event
        var @event = new TransactionInitiatedEvent
        {
            TransactionId = transactionId,
            FromAccountId = fromAccountId,
            ToAccountId = toAccountId,
            Amount = amount,
            Currency = currency,
            TransactionType = TransactionType.Transfer,
            IdempotencyKey = idempotencyKey
        };

        await _eventStore.AppendEventAsync(@event, cancellationToken);
        await _eventStore.PublishEventToKafkaAsync(@event, _kafkaSettings.TransactionEventsTopic, cancellationToken);

        return transaction;
    }

    private async Task<bool> DebitAccountAsync(Guid accountId, decimal amount, Transaction transaction, CancellationToken cancellationToken)
    {
        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == accountId && !a.IsDeleted, cancellationToken);

        if (account == null)
        {
            _logger.LogWarning("Account not found: {AccountId}", accountId);
            return false;
        }

        if (account.Status != AccountStatus.Active)
        {
            _logger.LogWarning("Account not active: {AccountId}, Status: {Status}", accountId, account.Status);
            return false;
        }

        if (account.AvailableBalance < amount)
        {
            _logger.LogWarning("Insufficient funds in account: {AccountId}, Available: {Balance}, Required: {Amount}",
                accountId, account.AvailableBalance, amount);
            return false;
        }

        // Debit with optimistic locking
        var previousBalance = account.Balance;
        account.Balance -= amount;
        account.AvailableBalance -= amount;
        account.LastTransactionDate = DateTime.UtcNow;
        account.UpdatedAt = DateTime.UtcNow;
        
        transaction.Status = TransactionStatus.Processing;
        transaction.ProcessedAt = DateTime.UtcNow;

        try
        {
            // Save both account and transaction changes in one operation
            await _context.SaveChangesAsync(cancellationToken);

            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            _logger.LogWarning("Concurrency conflict detected for account: {AccountId}", accountId);
            return false;
        }
        catch (DbUpdateException dbEx)
        {
            _logger.LogError(dbEx, "Database error while debiting account: {AccountId}. Inner: {Inner}", 
                accountId, dbEx.InnerException?.Message);
            throw new InvalidOperationException($"Database error: {dbEx.InnerException?.Message ?? dbEx.Message}", dbEx);
        }
    }

    private async Task<bool> CreditAccountAsync(Guid accountId, decimal amount, Transaction transaction, CancellationToken cancellationToken)
    {
        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == accountId && !a.IsDeleted, cancellationToken);

        if (account == null)
        {
            _logger.LogWarning("Destination account not found: {AccountId}", accountId);
            return false;
        }

        if (account.Status != AccountStatus.Active)
        {
            _logger.LogWarning("Destination account not active: {AccountId}", accountId);
            return false;
        }

        // Credit with optimistic locking
        account.Balance += amount;
        account.AvailableBalance += amount;
        account.LastTransactionDate = DateTime.UtcNow;
        account.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            _logger.LogWarning("Concurrency conflict detected for destination account: {AccountId}", accountId);
            return false;
        }
        catch (DbUpdateException dbEx)
        {
            _logger.LogError(dbEx, "Database error while crediting account: {AccountId}. Inner: {Inner}", 
                accountId, dbEx.InnerException?.Message);
            throw new InvalidOperationException($"Database error: {dbEx.InnerException?.Message ?? dbEx.Message}", dbEx);
        }
    }

    private async Task CompleteTransactionAsync(Transaction transaction, CancellationToken cancellationToken)
    {
        // Reload transaction from database to get fresh xmin value
        var freshTransaction = await _context.Transactions.FindAsync(new object[] { transaction.Id }, cancellationToken);
        if (freshTransaction == null)
        {
            _logger.LogError("Transaction not found when trying to complete: {TransactionId}", transaction.Id);
            throw new InvalidOperationException($"Transaction {transaction.Id} not found");
        }
        
        freshTransaction.Status = TransactionStatus.Completed;
        freshTransaction.CompletedAt = DateTime.UtcNow;
        freshTransaction.UpdatedAt = DateTime.UtcNow;
        
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException dbEx)
        {
            _logger.LogError(dbEx, "Database error while completing transaction: {TransactionId}. Inner: {Inner}", 
                transaction.Id, dbEx.InnerException?.Message);
            throw new InvalidOperationException($"Database error: {dbEx.InnerException?.Message ?? dbEx.Message}", dbEx);
        }

        // Publish completion event
        var @event = new TransactionCompletedEvent
        {
            TransactionId = freshTransaction.Id,
            PreviousBalance = 0, // Should be set from actual balance
            NewBalance = 0, // Should be set from actual balance
            CompletedBy = "System"
        };

        try
        {
            await _eventStore.AppendEventAsync(@event, cancellationToken);
            await _eventStore.PublishEventToKafkaAsync(@event, _kafkaSettings.TransactionEventsTopic, cancellationToken);
        }
        catch (Exception eventEx)
        {
            // Log but don't fail the saga if event publishing fails
            _logger.LogWarning(eventEx, "Failed to publish completion event for transaction: {TransactionId}", freshTransaction.Id);
        }
    }

    private async Task CompensateDebitAsync(Guid accountId, decimal amount, Transaction transaction, CancellationToken cancellationToken)
    {
        var account = await _context.Accounts.FindAsync(new object[] { accountId }, cancellationToken);
        if (account != null)
        {
            account.Balance += amount;
            account.AvailableBalance += amount;
            account.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Compensating debit for account {AccountId}, refunded {Amount}", accountId, amount);
        }
    }

    private async Task CompensateTransactionAsync(Transaction transaction, string reason, CancellationToken cancellationToken)
    {
        transaction.Status = TransactionStatus.Failed;
        transaction.FailureReason = reason;
        transaction.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        // Publish failure event
        var @event = new TransactionFailedEvent
        {
            TransactionId = transaction.Id,
            FailureReason = reason,
            ErrorCode = "SAGA_COMPENSATION"
        };

        await _eventStore.AppendEventAsync(@event, cancellationToken);
        await _eventStore.PublishEventToKafkaAsync(@event, _kafkaSettings.TransactionEventsTopic, cancellationToken);
    }
}
