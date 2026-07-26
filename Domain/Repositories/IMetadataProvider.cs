using Domain.Entities;

namespace Domain.Repositories;

public interface IMetadataProvider
{
    Task<MediaMetadata> GetMetadataAsync(
        MediaSource source,
        CancellationToken cancellationToken = default);
}