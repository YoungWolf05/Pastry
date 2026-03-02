using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PastryManager.Domain.Entities;
using PastryManager.Domain.Enums;
using PastryManager.Infrastructure.Data;
using PastryManager.Infrastructure.Services.Saga;

namespace PastryManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TransactionsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ISagaOrchestrator _sagaOrchestrator;
    private readonly ILogger<TransactionsController> _logger;

    public TransactionsController(
        ApplicationDbContext context,
        ISagaOrchestrator sagaOrchestrator,
        ILogger<TransactionsController> logger)
    {
        _context = context;
        _sagaOrchestrator = sagaOrchestrator;
        _logger = logger;
    }

    /// <summary>
    /// Execute a money transfer between accounts (uses Saga pattern)
    /// </summary>
    [HttpPost("transfer")]
    public async Task<ActionResult<TransactionDto>> Transfer([FromBody] TransferRequest request)
    {
        try
        {
            _logger.LogInformation("Transfer request: {IdempotencyKey} from {FromAccount} to {ToAccount} amount {Amount}", 
                request.IdempotencyKey, request.FromAccountId, request.ToAccountId, request.Amount);

            // Validate accounts exist
            var fromAccount = await _context.Accounts.FindAsync(request.FromAccountId);
            var toAccount = await _context.Accounts.FindAsync(request.ToAccountId);

            if (fromAccount == null)
                return NotFound(new { error = "Source account not found" });

            if (toAccount == null)
                return NotFound(new { error = "Destination account not found" });

            if (fromAccount.Status != AccountStatus.Active)
                return BadRequest(new { error = "Source account is not active" });

            if (toAccount.Status != AccountStatus.Active)
                return BadRequest(new { error = "Destination account is not active" });

            // Execute saga
            var (success, transactionId, errorMessage) = await _sagaOrchestrator.ExecuteTransferSagaAsync(
                request.FromAccountId,
                request.ToAccountId,
                request.Amount,
                request.Currency,
                request.IdempotencyKey
            );

            if (!success)
            {
                return BadRequest(new { 
                    error = errorMessage ?? "Transfer failed",
                    transactionId = transactionId
                });
            }

            var transaction = await _context.Transactions
                .FirstOrDefaultAsync(t => t.Id == transactionId);

            _logger.LogInformation("Transfer completed successfully: {TransactionId}", transactionId);

            return Ok(new TransactionDto
            {
                Id = transaction!.Id,
                FromAccountId = transaction.FromAccountId,
                ToAccountId = transaction.ToAccountId,
                Amount = transaction.Amount,
                Currency = transaction.Currency,
                TransactionType = transaction.TransactionType,
                Status = transaction.Status,
                Description = transaction.Description,
                CreatedAt = transaction.CreatedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Transfer failed: {IdempotencyKey}", request.IdempotencyKey);
            var innerMessage = ex.InnerException?.Message ?? ex.Message;
            return StatusCode(500, new { error = "Transfer failed", details = innerMessage, fullError = ex.ToString() });
        }
    }

    /// <summary>
    /// Get transaction by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<TransactionDto>> GetTransaction(Guid id)
    {
        try
        {
            var transaction = await _context.Transactions
                .Include(t => t.FromAccount)
                .Include(t => t.ToAccount)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (transaction == null)
                return NotFound(new { error = "Transaction not found" });

            return Ok(new TransactionDto
            {
                Id = transaction.Id,
                FromAccountId = transaction.FromAccountId,
                ToAccountId = transaction.ToAccountId,
                Amount = transaction.Amount,
                Currency = transaction.Currency,
                TransactionType = transaction.TransactionType,
                Status = transaction.Status,
                Description = transaction.Description,
                RiskScore = transaction.RiskScore,
                Fee = transaction.Fee,
                CreatedAt = transaction.CreatedAt,
                CompletedAt = transaction.CompletedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get transaction {TransactionId}", id);
            return StatusCode(500, new { error = "Failed to retrieve transaction", details = ex.Message });
        }
    }

    /// <summary>
    /// Get account transaction history
    /// </summary>
    [HttpGet("account/{accountId}")]
    public async Task<ActionResult<List<TransactionDto>>> GetAccountTransactions(
        Guid accountId, 
        [FromQuery] int page = 1, 
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var transactions = await _context.Transactions
                .Where(t => t.FromAccountId == accountId || t.ToAccountId == accountId)
                .OrderByDescending(t => t.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(t => new TransactionDto
                {
                    Id = t.Id,
                    FromAccountId = t.FromAccountId,
                    ToAccountId = t.ToAccountId,
                    Amount = t.Amount,
                    Currency = t.Currency,
                    TransactionType = t.TransactionType,
                    Status = t.Status,
                    Description = t.Description,
                    Fee = t.Fee,
                    CreatedAt = t.CreatedAt,
                    CompletedAt = t.CompletedAt
                })
                .ToListAsync();

            return Ok(transactions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get transactions for account {AccountId}", accountId);
            return StatusCode(500, new { error = "Failed to retrieve transactions", details = ex.Message });
        }
    }

    /// <summary>
    /// Deposit money into an account (for testing)
    /// </summary>
    [HttpPost("deposit")]
    public async Task<ActionResult<TransactionDto>> Deposit([FromBody] DepositRequest request)
    {
        try
        {
            var account = await _context.Accounts.FindAsync(request.AccountId);
            if (account == null)
                return NotFound(new { error = "Account not found" });

            if (account.Status != AccountStatus.Active)
                return BadRequest(new { error = "Account is not active" });

            var transaction = new Transaction
            {
                Id = Guid.NewGuid(),
                IdempotencyKey = request.IdempotencyKey,
                FromAccountId = request.AccountId, // Same as ToAccountId for deposits
                ToAccountId = request.AccountId,
                Amount = request.Amount,
                Currency = request.Currency,
                TransactionType = TransactionType.Deposit,
                Status = TransactionStatus.Completed,
                Description = request.Description ?? "Deposit",
                CompletedAt = DateTime.UtcNow
            };

            account.Balance += request.Amount;
            account.AvailableBalance += request.Amount;
            account.LastTransactionDate = DateTime.UtcNow;

            _context.Transactions.Add(transaction);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Deposit completed: {Amount} to account {AccountId}", request.Amount, request.AccountId);

            return Ok(new TransactionDto
            {
                Id = transaction.Id,
                ToAccountId = transaction.ToAccountId,
                Amount = transaction.Amount,
                Currency = transaction.Currency,
                TransactionType = transaction.TransactionType,
                Status = transaction.Status,
                Description = transaction.Description,
                CreatedAt = transaction.CreatedAt,
                CompletedAt = transaction.CompletedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Deposit failed for account {AccountId}", request.AccountId);
            return StatusCode(500, new { error = "Deposit failed", details = ex.Message });
        }
    }
}

// DTOs
public record TransferRequest(
    string IdempotencyKey,
    Guid FromAccountId,
    Guid ToAccountId,
    decimal Amount,
    string Currency,
    string? Description = null,
    string? DeviceFingerprint = null
);

public record DepositRequest(
    string IdempotencyKey,
    Guid AccountId,
    decimal Amount,
    string Currency,
    string? Description = null
);

public record TransactionDto
{
    public Guid Id { get; init; }
    public Guid? FromAccountId { get; init; }
    public Guid? ToAccountId { get; init; }
    public decimal Amount { get; init; }
    public string Currency { get; init; } = string.Empty;
    public TransactionType TransactionType { get; init; }
    public TransactionStatus Status { get; init; }
    public string? Description { get; init; }
    public decimal? RiskScore { get; init; }
    public decimal? Fee { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
}
