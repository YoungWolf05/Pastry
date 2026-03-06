using MediatR;
using PastryManager.Application.Common.Models;
using PastryManager.Application.Transactions.DTOs;

namespace PastryManager.Application.Transactions.Commands.Deposit;

public record DepositCommand(
    string IdempotencyKey,
    Guid AccountId,
    decimal Amount,
    string Currency,
    string? Description = null
) : IRequest<Result<TransactionDto>>;
