using Domain.Entities;

namespace Domain.Repositories;

public interface IMetadataWriter
{
    Task WriteMetadataAsync(
        DownloadJob job,
        CancellationToken cancellationToken = default);
}
