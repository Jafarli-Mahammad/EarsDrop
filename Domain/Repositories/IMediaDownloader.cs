using Domain.Entities;

namespace Domain.Repositories;

public interface IMediaDownloader
{
    Task DownloadAsync(
        DownloadJob job,
        CancellationToken cancellationToken = default);
}