using FluentValidation;

namespace Application.UseCases.CompleteDownload;

public class CompleteDownloadValidator : AbstractValidator<CompleteDownloadCommand>
{
    public CompleteDownloadValidator()
    {
        RuleFor(x => x.JobId)
            .NotEmpty().WithMessage("JobId cannot be empty.");
    }
}
