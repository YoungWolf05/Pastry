using FluentValidation;

namespace PastryManager.Application.TaskRequests.Commands.CreateTaskRequest;

public class CreateTaskRequestCommandValidator : AbstractValidator<CreateTaskRequestCommand>
{
    public CreateTaskRequestCommandValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required")
            .MaximumLength(200).WithMessage("Title must not exceed 200 characters");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required")
            .MaximumLength(2000).WithMessage("Description must not exceed 2000 characters");

        RuleFor(x => x.Priority)
            .IsInEnum().WithMessage("Invalid priority value");

        RuleFor(x => x.CreatedByUserId)
            .NotEmpty().WithMessage("Creator user ID is required");

        RuleFor(x => x.AssignedToUserId)
            .NotEmpty().WithMessage("Assigned user ID is required");

        RuleFor(x => x.DueDate)
            .GreaterThan(DateTime.UtcNow)
            .When(x => x.DueDate.HasValue)
            .WithMessage("Due date must be in the future");
    }
}
