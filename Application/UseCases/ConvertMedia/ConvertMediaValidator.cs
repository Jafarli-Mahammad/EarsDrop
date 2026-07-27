using FluentValidation;

namespace Application.UseCases.ConvertMedia;

public class ConvertMediaValidator : AbstractValidator<ConvertMediaCommand>
{
    public ConvertMediaValidator()
    {
        RuleFor(x => x.JobId)
            .NotEmpty().WithMessage("JobId cannot be empty.");
    }
}
