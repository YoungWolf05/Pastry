using MediatR;
using Microsoft.EntityFrameworkCore;
using PastryManager.Application.Common.Interfaces;
using PastryManager.Application.Common.Models;
using PastryManager.Application.Transactions.Commands.ExecuteTransfer;
using PastryManager.Application.Transactions.DTOs;

namespace PastryManager.Application.Transactions.Queries.GetTransactionById;

public class GetTransactionByIdQueryHandler : IRequestHandler<GetTransactionByIdQuery, Result<TransactionDto>>
{
    private readonly IApplicationDbContext _context;

    public GetTransactionByIdQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<Result<TransactionDto>> Handle(GetTransactionByIdQuery request, CancellationToken cancellationToken)
    {
        var transaction = await _context.Transactions
            .FirstOrDefaultAsync(t => t.Id == request.TransactionId, cancellationToken);

        if (transaction is null)
            return Result<TransactionDto>.Failure("Transaction not found");

        return Result<TransactionDto>.Success(ExecuteTransferCommandHandler.Map(transaction));
    }
}
