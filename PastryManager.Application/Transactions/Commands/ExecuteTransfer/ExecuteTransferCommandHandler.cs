using MediatR;
using Microsoft.EntityFrameworkCore;
using PastryManager.Application.Common.Interfaces;
using PastryManager.Application.Common.Models;
using PastryManager.Application.Transactions.DTOs;
using PastryManager.Domain.Entities;
using PastryManager.Domain.Enums;

namespace PastryManager.Application.Transactions.Commands.ExecuteTransfer;

public class ExecuteTransferCommandHandler : IRequestHandler<ExecuteTransferCommand, Result<TransactionDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ISagaOrchestrator _saga;

    public ExecuteTransferCommandHandler(IApplicationDbContext context, ISagaOrchestrator saga)
    {
        _context = context;
        _saga = saga;
    }

    public async Task<Result<TransactionDto>> Handle(ExecuteTransferCommand request, CancellationToken cancellationToken)
    {
        var fromAccount = await _context.Accounts.FindAsync(new object[] { request.FromAccountId }, cancellationToken);
        if (fromAccount is null)
            return Result<TransactionDto>.Failure("Source account not found");

        var toAccount = await _context.Accounts.FindAsync(new object[] { request.ToAccountId }, cancellationToken);
        if (toAccount is null)
            return Result<TransactionDto>.Failure("Destination account not found");

        if (fromAccount.Status != AccountStatus.Active)
            return Result<TransactionDto>.Failure("Source account is not active");

        if (toAccount.Status != AccountStatus.Active)
            return Result<TransactionDto>.Failure("Destination account is not active");

        var (success, transactionId, errorMessage) = await _saga.ExecuteTransferSagaAsync(
            request.FromAccountId, request.ToAccountId,
            request.Amount, request.Currency,
            request.IdempotencyKey, cancellationToken);

        if (!success)
            return Result<TransactionDto>.Failure(errorMessage ?? "Transfer failed");

        var transaction = await _context.Transactions
            .FirstOrDefaultAsync(t => t.Id == transactionId, cancellationToken);

        return Result<TransactionDto>.Success(Map(transaction!));
    }

    internal static TransactionDto Map(Transaction t) => new(
        t.Id, t.FromAccountId, t.ToAccountId,
        t.Amount, t.Currency,
        t.TransactionType, t.Status,
        t.Description, t.RiskScore, t.Fee,
        t.CreatedAt, t.CompletedAt);
}
