using FluentValidation;

namespace PastryManager.Application.Transactions.Commands.ExecuteTransfer;

public class ExecuteTransferCommandValidator : AbstractValidator<ExecuteTransferCommand>
{
    public ExecuteTransferCommandValidator()
    {
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(200);
        RuleFor(x => x.FromAccountId).NotEmpty();
        RuleFor(x => x.ToAccountId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("Transfer amount must be greater than zero");
        RuleFor(x => x.Currency).NotEmpty().Length(3).WithMessage("Currency must be a 3-character ISO code");
        RuleFor(x => x.FromAccountId)
            .NotEqual(x => x.ToAccountId)
            .WithMessage("Source and destination accounts must be different");
    }
}
