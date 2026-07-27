using FluentValidation;

namespace Application.UseCases.GetDownloadHistory;

public class GetDownloadHistoryValidator : AbstractValidator<GetDownloadHistoryQuery>
{
    public GetDownloadHistoryValidator()
    {
    }
}
