using MediatR;
using PastryManager.Application.Common.Models;

namespace PastryManager.Application.Transactions.Commands.SimulateDlq;

public record SimulateDlqCommand : IRequest<Result<SimulateDlqResult>>;

public record SimulateDlqResult(
    string Message,
    string Topic,
    string Key,
    object Payload
);
