using FluentValidation;
using PastryManager.Domain.Enums;

namespace PastryManager.Application.Files.Commands.UploadFile;

public class UploadFileCommandValidator : AbstractValidator<UploadFileCommand>
{
    public UploadFileCommandValidator()
    {
        RuleFor(x => x.FileStream)
            .NotNull()
            .WithMessage("File stream is required");

        RuleFor(x => x.FileName)
            .NotEmpty()
            .WithMessage("File name is required");

        RuleFor(x => x.FileSizeBytes)
            .GreaterThan(0)
            .WithMessage("File cannot be empty");

        RuleFor(x => x.EntityType)
            .IsInEnum()
            .WithMessage("Invalid entity type");

        RuleFor(x => x.EntityId)
            .NotEmpty()
            .WithMessage("Entity ID is required");

        RuleFor(x => x.UploadedBy)
            .NotEmpty()
            .WithMessage("Uploaded by user ID is required");
    }
}
