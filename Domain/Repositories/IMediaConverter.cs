using Domain.Entities;

namespace Domain.Repositories;

public interface IMediaConverter
{
    Task ConvertAsync(
        DownloadJob job,
        CancellationToken cancellationToken = default);
}