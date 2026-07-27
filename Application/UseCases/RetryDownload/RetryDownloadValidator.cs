using FluentValidation;

namespace Application.UseCases.RetryDownload;

public class RetryDownloadValidator : AbstractValidator<RetryDownloadCommand>
{
    public RetryDownloadValidator()
    {
        RuleFor(x => x.JobId)
            .NotEmpty().WithMessage("JobId cannot be empty.");
    }
}
