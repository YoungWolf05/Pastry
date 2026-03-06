using MediatR;
using PastryManager.Application.Common.Interfaces;
using PastryManager.Application.Common.Models;

namespace PastryManager.Application.Transactions.Commands.SimulateDlq;

public class SimulateDlqCommandHandler : IRequestHandler<SimulateDlqCommand, Result<SimulateDlqResult>>
{
    private const string DeadLetterTopic = "dead-letter-queue";

    private readonly IEventPublisher _publisher;

    public SimulateDlqCommandHandler(IEventPublisher publisher) => _publisher = publisher;

    public async Task<Result<SimulateDlqResult>> Handle(SimulateDlqCommand request, CancellationToken cancellationToken)
    {
        var key = Guid.NewGuid().ToString();
        var payload = new
        {
            originalTopic = "transaction-events",
            key,
            message = new { transactionId = Guid.NewGuid(), amount = 9999.99, currency = "USD" },
            errorReason = "Simulated consumer processing failure: downstream service timeout",
            failedAt = DateTimeOffset.UtcNow,
            retryCount = 3
        };

        await _publisher.PublishAsync(DeadLetterTopic, key, payload, cancellationToken);

        return Result<SimulateDlqResult>.Success(
            new SimulateDlqResult("Dead letter message published", DeadLetterTopic, key, payload));
    }
}
