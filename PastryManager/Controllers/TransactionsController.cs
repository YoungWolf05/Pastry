using MediatR;
using Microsoft.AspNetCore.Mvc;
using PastryManager.Application.Transactions.Commands.Deposit;
using PastryManager.Application.Transactions.Commands.ExecuteTransfer;
using PastryManager.Application.Transactions.Commands.SimulateDlq;
using PastryManager.Application.Transactions.Commands.SimulateSagaFailure;
using PastryManager.Application.Transactions.Queries.GetAccountTransactions;
using PastryManager.Application.Transactions.Queries.GetTransactionById;

namespace PastryManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TransactionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public TransactionsController(IMediator mediator) => _mediator = mediator;

    /// <summary>Execute a money transfer between accounts (Saga pattern)</summary>
    [HttpPost("transfer")]
    public async Task<IActionResult> Transfer([FromBody] ExecuteTransferCommand command)
    {
        var result = await _mediator.Send(command);
        return result.IsSuccess
            ? Ok(result.Data)
            : BadRequest(new { errors = result.Errors, error = result.Error });
    }

    /// <summary>Deposit money into an account</summary>
    [HttpPost("deposit")]
    public async Task<IActionResult> Deposit([FromBody] DepositCommand command)
    {
        var result = await _mediator.Send(command);
        return result.IsSuccess
            ? Ok(result.Data)
            : BadRequest(new { errors = result.Errors, error = result.Error });
    }

    /// <summary>Get transaction by ID</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetTransaction(Guid id)
    {
        var result = await _mediator.Send(new GetTransactionByIdQuery(id));
        return result.IsSuccess ? Ok(result.Data) : NotFound(new { error = result.Error });
    }

    /// <summary>Get account transaction history</summary>
    [HttpGet("account/{accountId:guid}")]
    public async Task<IActionResult> GetAccountTransactions(
        Guid accountId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var result = await _mediator.Send(new GetAccountTransactionsQuery(accountId, page, pageSize));
        return result.IsSuccess ? Ok(result.Data) : NotFound(new { error = result.Error });
    }

    /// <summary>[TEST ONLY] Publish a simulated failed event to the dead-letter-queue topic</summary>
    [HttpPost("test/simulate-dlq")]
    public async Task<IActionResult> SimulateDeadLetterQueue()
    {
        var result = await _mediator.Send(new SimulateDlqCommand());
        return Ok(result.Data);
    }

    /// <summary>[TEST ONLY] Trigger saga failure + compensation - populates transfer-saga-events</summary>
    [HttpPost("test/simulate-saga-failure")]
    public async Task<IActionResult> SimulateSagaFailure([FromQuery] Guid fromAccountId)
    {
        var result = await _mediator.Send(new SimulateSagaFailureCommand(fromAccountId));
        return Ok(result.Data);
    }
}
