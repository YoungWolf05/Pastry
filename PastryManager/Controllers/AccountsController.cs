using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PastryManager.Domain.Entities;
using PastryManager.Domain.Enums;
using PastryManager.Domain.Events;
using PastryManager.Infrastructure.Data;
using PastryManager.Infrastructure.Services.Encryption;
using PastryManager.Infrastructure.Services.EventSourcing;

namespace PastryManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AccountsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IEncryptionService _encryptionService;
    private readonly IEventStoreService _eventStoreService;
    private readonly ILogger<AccountsController> _logger;

    public AccountsController(
        ApplicationDbContext context,
        IEncryptionService encryptionService,
        IEventStoreService eventStoreService,
        ILogger<AccountsController> logger)
    {
        _context = context;
        _encryptionService = encryptionService;
        _eventStoreService = eventStoreService;
        _logger = logger;
    }

    /// <summary>
    /// Create a new bank account
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<AccountDto>> CreateAccount([FromBody] CreateAccountRequest request)
    {
        try
        {
            // Generate unique account number
            var accountNumber = GenerateAccountNumber();
            var encryptedAccountNumber = await _encryptionService.EncryptAsync(accountNumber);

            var account = new Account
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId ?? Guid.NewGuid(), // In real app, get from JWT token
                AccountNumber = encryptedAccountNumber,
                AccountType = request.AccountType,
                Currency = request.Currency,
                Balance = request.InitialBalance,
                AvailableBalance = request.InitialBalance,
                Status = AccountStatus.Active,
                DailyTransferLimit = 10000,
                MonthlyTransferLimit = 50000,
                LastTransactionDate = DateTime.UtcNow
            };

            if (!string.IsNullOrEmpty(request.IBAN))
            {
                account.IBAN = await _encryptionService.EncryptAsync(request.IBAN);
            }

            if (!string.IsNullOrEmpty(request.SwiftCode))
            {
                account.SwiftCode = await _encryptionService.EncryptAsync(request.SwiftCode);
            }

            _context.Accounts.Add(account);
            await _context.SaveChangesAsync();

            // Publish event
            var accountCreatedEvent = new AccountCreatedEvent
            {
                EventId = Guid.NewGuid(),
                OccurredAt = DateTime.UtcNow,
                AccountId = account.Id,
                UserId = account.UserId,
                AccountNumber = accountNumber,
                AccountType = account.AccountType,
                Currency = account.Currency,
                InitialBalance = account.Balance
            };

            await _eventStoreService.AppendEventAsync(accountCreatedEvent);

            _logger.LogInformation("Account created: {AccountId}", account.Id);

            return CreatedAtAction(nameof(GetAccount), new { id = account.Id }, new AccountDto
            {
                Id = account.Id,
                AccountNumber = accountNumber,
                AccountType = account.AccountType,
                Currency = account.Currency,
                Balance = account.Balance,
                AvailableBalance = account.AvailableBalance,
                Status = account.Status,
                CreatedAt = account.CreatedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create account");
            var innerMessage = ex.InnerException?.Message ?? ex.Message;
            return StatusCode(500, new { error = "Failed to create account", details = innerMessage, fullError = ex.ToString() });
        }
    }

    /// <summary>
    /// Get account by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<AccountDto>> GetAccount(Guid id)
    {
        try
        {
            var account = await _context.Accounts
                .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted);

            if (account == null)
                return NotFound(new { error = "Account not found" });

            var decryptedAccountNumber = await _encryptionService.DecryptAsync(account.AccountNumber);

            return Ok(new AccountDto
            {
                Id = account.Id,
                AccountNumber = decryptedAccountNumber,
                AccountType = account.AccountType,
                Currency = account.Currency,
                Balance = account.Balance,
                AvailableBalance = account.AvailableBalance,
                Status = account.Status,
                DailyTransferLimit = account.DailyTransferLimit,
                MonthlyTransferLimit = account.MonthlyTransferLimit,
                CreatedAt = account.CreatedAt,
                UpdatedAt = account.UpdatedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get account {AccountId}", id);
            return StatusCode(500, new { error = "Failed to retrieve account", details = ex.Message });
        }
    }

    /// <summary>
    /// Get all accounts for a user
    /// </summary>
    [HttpGet("user/{userId}")]
    public async Task<ActionResult<List<AccountDto>>> GetUserAccounts(Guid userId)
    {
        try
        {
            var accounts = await _context.Accounts
                .Where(a => a.UserId == userId && !a.IsDeleted)
                .ToListAsync();

            var accountDtos = new List<AccountDto>();
            foreach (var account in accounts)
            {
                var decryptedAccountNumber = await _encryptionService.DecryptAsync(account.AccountNumber);
                accountDtos.Add(new AccountDto
                {
                    Id = account.Id,
                    AccountNumber = decryptedAccountNumber,
                    AccountType = account.AccountType,
                    Currency = account.Currency,
                    Balance = account.Balance,
                    AvailableBalance = account.AvailableBalance,
                    Status = account.Status,
                    CreatedAt = account.CreatedAt
                });
            }

            return Ok(accountDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get accounts for user {UserId}", userId);
            return StatusCode(500, new { error = "Failed to retrieve accounts", details = ex.Message });
        }
    }

    /// <summary>
    /// Update account status
    /// </summary>
    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateAccountStatus(Guid id, [FromBody] UpdateAccountStatusRequest request)
    {
        try
        {
            var account = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == id);
            if (account == null)
                return NotFound(new { error = "Account not found" });

            var oldStatus = account.Status;
            account.Status = request.Status;
            await _context.SaveChangesAsync();

            // Publish event based on new status
            if (request.Status == AccountStatus.Suspended)
            {
                await _eventStoreService.AppendEventAsync(new AccountSuspendedEvent
                {
                    EventId = Guid.NewGuid(),
                    OccurredAt = DateTime.UtcNow,
                    AccountId = account.Id,
                    UserId = account.UserId,
                    Reason = request.Reason ?? "Administrative action",
                    SuspendedBy = "System" // In production, get from JWT token
                });
            }
            else if (request.Status == AccountStatus.Active && oldStatus != AccountStatus.Active)
            {
                await _eventStoreService.AppendEventAsync(new AccountActivatedEvent
                {
                    EventId = Guid.NewGuid(),
                    OccurredAt = DateTime.UtcNow,
                    AccountId = account.Id,
                    UserId = account.UserId,
                    ActivatedBy = "System" // In production, get from JWT token
                });
            }

            _logger.LogInformation("Account {AccountId} status updated from {OldStatus} to {NewStatus}", 
                id, oldStatus, request.Status);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update account status {AccountId}", id);
            return StatusCode(500, new { error = "Failed to update account status", details = ex.Message });
        }
    }

    private string GenerateAccountNumber()
    {
        // Simple account number generation (in production, use proper bank account number format)
        return $"ACC{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}{Random.Shared.Next(1000, 9999)}";
    }
}

// DTOs
public record CreateAccountRequest(
    Guid? UserId,
    AccountType AccountType,
    string Currency,
    decimal InitialBalance,
    string? IBAN = null,
    string? SwiftCode = null
);

public record AccountDto
{
    public Guid Id { get; init; }
    public string AccountNumber { get; init; } = string.Empty;
    public AccountType AccountType { get; init; }
    public string Currency { get; init; } = string.Empty;
    public decimal Balance { get; init; }
    public decimal AvailableBalance { get; init; }
    public AccountStatus Status { get; init; }
    public decimal DailyTransferLimit { get; init; }
    public decimal MonthlyTransferLimit { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public record UpdateAccountStatusRequest(
    AccountStatus Status,
    string? Reason = null
);
