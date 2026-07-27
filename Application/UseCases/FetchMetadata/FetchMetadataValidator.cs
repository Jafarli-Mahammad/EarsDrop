using FluentValidation;

namespace Application.UseCases.FetchMetadata;

public class FetchMetadataValidator : AbstractValidator<FetchMetadataCommand>
{
    public FetchMetadataValidator()
    {
        RuleFor(x => x.JobId)
            .NotEmpty().WithMessage("JobId cannot be empty.");
    }
}
