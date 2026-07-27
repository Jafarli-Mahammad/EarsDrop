using FluentValidation;

namespace Application.UseCases.WriteMetadata;

public class WriteMetadataValidator : AbstractValidator<WriteMetadataCommand>
{
    public WriteMetadataValidator()
    {
        RuleFor(x => x.JobId)
            .NotEmpty().WithMessage("JobId cannot be empty.");
    }
}
