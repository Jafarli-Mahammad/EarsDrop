using FluentValidation;

namespace Application.UseCases.DeleteDownload;

public class DeleteDownloadValidator : AbstractValidator<DeleteDownloadCommand>
{
    public DeleteDownloadValidator()
    {
        RuleFor(x => x.JobId)
            .NotEmpty().WithMessage("JobId cannot be empty.");
    }
}
