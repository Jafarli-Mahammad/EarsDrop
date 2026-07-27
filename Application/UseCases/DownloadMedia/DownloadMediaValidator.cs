using FluentValidation;

namespace Application.UseCases.DownloadMedia;

public class DownloadMediaValidator : AbstractValidator<DownloadMediaCommand>
{
    public DownloadMediaValidator()
    {
        RuleFor(x => x.Url)
            .NotEmpty().WithMessage("Media URL cannot be empty.")
            .Must(BeAValidUrl).WithMessage("Invalid media URL specified.");

        RuleFor(x => x.OutputFormat)
            .IsInEnum().WithMessage("Invalid output format specified.");
    }

    private bool BeAValidUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uriResult)
               && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
    }
}
