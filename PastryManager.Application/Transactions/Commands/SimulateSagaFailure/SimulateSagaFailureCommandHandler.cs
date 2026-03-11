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
        // Trigger debit failure by using an amount that exceeds any realistic balance.
        // Both account IDs must be real accounts (FK constraint), so we use fromAccountId
        // for both ends — the debit will fail with "insufficient funds", compensation runs,
        // and the SagaFailed event is emitted. Same observable outcome without FK violations.
        var idempotencyKey = $"test-saga-fail-{Guid.NewGuid()}";

        var (success, transactionId, errorMessage) = await _saga.ExecuteTransferSagaAsync(
            request.FromAccountId, request.FromAccountId,
            amount: 999_999_999m, currency: "USD",
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
