using Domain.Entities;

namespace Domain.Repositories;

public interface IDownloadJobRepository
{
    Task AddAsync(
        DownloadJob job,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        DownloadJob job,
        CancellationToken cancellationToken = default);

    Task<DownloadJob?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DownloadJob>> GetAllAsync(
        CancellationToken cancellationToken = default);    
}