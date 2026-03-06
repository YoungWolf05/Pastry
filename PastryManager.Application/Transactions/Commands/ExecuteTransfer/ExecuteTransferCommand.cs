using MediatR;
using PastryManager.Application.Common.Models;
using PastryManager.Application.Transactions.DTOs;

namespace PastryManager.Application.Transactions.Commands.ExecuteTransfer;

public record ExecuteTransferCommand(
    string IdempotencyKey,
    Guid FromAccountId,
    Guid ToAccountId,
    decimal Amount,
    string Currency,
    string? Description = null
) : IRequest<Result<TransactionDto>>;
