using MediatR;
using Microsoft.EntityFrameworkCore;
using PastryManager.Application.Common.Interfaces;
using PastryManager.Application.Common.Models;
using PastryManager.Application.Transactions.Commands.ExecuteTransfer;
using PastryManager.Application.Transactions.DTOs;

namespace PastryManager.Application.Transactions.Queries.GetAccountTransactions;

public class GetAccountTransactionsQueryHandler : IRequestHandler<GetAccountTransactionsQuery, Result<List<TransactionDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetAccountTransactionsQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<Result<List<TransactionDto>>> Handle(GetAccountTransactionsQuery request, CancellationToken cancellationToken)
    {
        var transactions = await _context.Transactions
            .Where(t => t.FromAccountId == request.AccountId || t.ToAccountId == request.AccountId)
            .OrderByDescending(t => t.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return Result<List<TransactionDto>>.Success(
            transactions.ConvertAll(ExecuteTransferCommandHandler.Map));
    }
}
