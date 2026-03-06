using MediatR;
using PastryManager.Application.Common.Interfaces;
using PastryManager.Application.Common.Models;
using PastryManager.Application.Transactions.DTOs;
using PastryManager.Domain.Entities;
using PastryManager.Domain.Enums;

namespace PastryManager.Application.Transactions.Commands.Deposit;

public class DepositCommandHandler : IRequestHandler<DepositCommand, Result<TransactionDto>>
{
    private readonly IApplicationDbContext _context;

    public DepositCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<Result<TransactionDto>> Handle(DepositCommand request, CancellationToken cancellationToken)
    {
        var account = await _context.Accounts.FindAsync(new object[] { request.AccountId }, cancellationToken);
        if (account is null)
            return Result<TransactionDto>.Failure("Account not found");

        if (account.Status != AccountStatus.Active)
            return Result<TransactionDto>.Failure("Account is not active");

        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            IdempotencyKey = request.IdempotencyKey,
            FromAccountId = request.AccountId,
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
        await _context.SaveChangesAsync(cancellationToken);

        return Result<TransactionDto>.Success(new TransactionDto(
            transaction.Id,
            transaction.FromAccountId,
            transaction.ToAccountId,
            transaction.Amount,
            transaction.Currency,
            transaction.TransactionType,
            transaction.Status,
            transaction.Description,
            null, null,
            transaction.CreatedAt,
            transaction.CompletedAt));
    }
}
