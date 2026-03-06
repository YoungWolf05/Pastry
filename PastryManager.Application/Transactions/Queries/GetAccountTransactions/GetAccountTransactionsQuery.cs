using MediatR;
using PastryManager.Application.Common.Models;
using PastryManager.Application.Transactions.DTOs;

namespace PastryManager.Application.Transactions.Queries.GetAccountTransactions;

public record GetAccountTransactionsQuery(
    Guid AccountId,
    int Page = 1,
    int PageSize = 50
) : IRequest<Result<List<TransactionDto>>>;
