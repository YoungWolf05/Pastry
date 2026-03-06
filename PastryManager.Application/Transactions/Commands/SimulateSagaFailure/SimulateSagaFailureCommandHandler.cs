using MediatR;
using PastryManager.Application.Common.Interfaces;
using PastryManager.Application.Common.Models;

namespace PastryManager.Application.Transactions.Commands.SimulateSagaFailure;

public class SimulateSagaFailureCommandHandler : IRequestHandler<SimulateSagaFailureCommand, Result<SimulateSagaFailureResult>>
{
    private const string SagaEventsTopic = "transfer-saga-events";

    private readonly ISagaOrchestrator _saga;

    public SimulateSagaFailureCommandHandler(ISagaOrchestrator saga) => _saga = saga;

    public async Task<Result<SimulateSagaFailureResult>> Handle(SimulateSagaFailureCommand request, CancellationToken cancellationToken)
    {
        // Use a non-existent toAccountId so the credit step fails, triggering compensation
        var fakeToAccountId = Guid.NewGuid();
        var idempotencyKey  = $"test-saga-fail-{Guid.NewGuid()}";

        var (success, transactionId, errorMessage) = await _saga.ExecuteTransferSagaAsync(
            request.FromAccountId, fakeToAccountId,
            amount: 0.01m, currency: "USD",
            idempotencyKey: idempotencyKey,
            cancellationToken: cancellationToken);

        return Result<SimulateSagaFailureResult>.Success(new SimulateSagaFailureResult(
            Message: "Saga failure simulation complete — check transfer-saga-events topic",
            Success: success,
            TransactionId: transactionId,
            ErrorMessage: errorMessage,
            ExpectedEvents: ["SagaStarted", "AccountDebited", "SagaFailed"],
            KafkaUiUrl: $"http://localhost:8081/ui/clusters/local/topics/{SagaEventsTopic}/messages"
        ));
    }
}
