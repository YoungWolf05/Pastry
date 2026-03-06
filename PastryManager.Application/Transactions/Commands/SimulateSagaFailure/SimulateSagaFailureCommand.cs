using MediatR;
using PastryManager.Application.Common.Models;

namespace PastryManager.Application.Transactions.Commands.SimulateSagaFailure;

public record SimulateSagaFailureCommand(Guid FromAccountId) : IRequest<Result<SimulateSagaFailureResult>>;

public record SimulateSagaFailureResult(
    string Message,
    bool Success,
    Guid? TransactionId,
    string? ErrorMessage,
    string[] ExpectedEvents,
    string KafkaUiUrl
);
