using MediatR;
using PastryManager.Application.Common.Models;
using PastryManager.Application.Transactions.DTOs;

namespace PastryManager.Application.Transactions.Queries.GetTransactionById;

public record GetTransactionByIdQuery(Guid TransactionId) : IRequest<Result<TransactionDto>>;
