using System.Reflection;
using System.Text.Json;
using Dapper;
using Domain.Entities;
using Domain.Enums;
using Domain.Repositories;
using Domain.Value_Objects;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Persistence;

public class DownloadJobRepository : IDownloadJobRepository
{
    private readonly DataContext _dataContext;
    private readonly ILogger<DownloadJobRepository> _logger;

    public DownloadJobRepository(
        DataContext dataContext,
        ILogger<DownloadJobRepository> logger)
    {
        _dataContext = dataContext;
        _logger = logger;
    }

    public async Task AddAsync(DownloadJob job, CancellationToken cancellationToken = default)
    {
        await _dataContext.InitializeDatabaseAsync(cancellationToken);

        _logger.LogInformation(
            "AddAsync: writing to SQLite at '{Path}'",
            _dataContext.DatabasePath);

        using var connection = _dataContext.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            INSERT INTO DownloadJobs (
                Id, Url, Platform, Title, Uploader, DurationTicks, ThumbnailUrl,
                OutputFormat, Status, OutputPath, MetadataJson, CreatedAt, CompletedAt, ErrorMessage
            ) VALUES (
                @Id, @Url, @Platform, @Title, @Uploader, @DurationTicks, @ThumbnailUrl,
                @OutputFormat, @Status, @OutputPath, @MetadataJson, @CreatedAt, @CompletedAt, @ErrorMessage
            );
            """;

        var param = MapToRow(job);
        await connection.ExecuteAsync(new CommandDefinition(sql, param, cancellationToken: cancellationToken));
        _logger.LogInformation("Saved download job {JobId} to SQLite database", job.Id);
    }

    public async Task UpdateAsync(DownloadJob job, CancellationToken cancellationToken = default)
    {
        await _dataContext.InitializeDatabaseAsync(cancellationToken);

        using var connection = _dataContext.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            UPDATE DownloadJobs SET
                Url = @Url,
                Platform = @Platform,
                Title = @Title,
                Uploader = @Uploader,
                DurationTicks = @DurationTicks,
                ThumbnailUrl = @ThumbnailUrl,
                OutputFormat = @OutputFormat,
                Status = @Status,
                OutputPath = @OutputPath,
                MetadataJson = @MetadataJson,
                CreatedAt = @CreatedAt,
                CompletedAt = @CompletedAt,
                ErrorMessage = @ErrorMessage
            WHERE Id = @Id;
            """;

        var param = MapToRow(job);
        await connection.ExecuteAsync(new CommandDefinition(sql, param, cancellationToken: cancellationToken));
        _logger.LogDebug("Updated download job {JobId} in SQLite database", job.Id);
    }

    public async Task<DownloadJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await _dataContext.InitializeDatabaseAsync(cancellationToken);

        using var connection = _dataContext.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = "SELECT * FROM DownloadJobs WHERE Id = @Id;";
        var row = await connection.QuerySingleOrDefaultAsync<DownloadJobRow>(
            new CommandDefinition(sql, new { Id = id.ToString() }, cancellationToken: cancellationToken));

        return row != null ? MapToEntity(row) : null;
    }

    public async Task<IReadOnlyList<DownloadJob>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await _dataContext.InitializeDatabaseAsync(cancellationToken);

        using var connection = _dataContext.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = "SELECT * FROM DownloadJobs ORDER BY CreatedAt DESC;";
        var rows = await connection.QueryAsync<DownloadJobRow>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));

        return rows.Select(MapToEntity).ToList();
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await _dataContext.InitializeDatabaseAsync(cancellationToken);

        using var connection = _dataContext.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = "DELETE FROM DownloadJobs WHERE Id = @Id;";
        await connection.ExecuteAsync(new CommandDefinition(sql, new { Id = id.ToString() }, cancellationToken: cancellationToken));
        _logger.LogInformation("Deleted download job {JobId} from SQLite database", id);
    }

    private static DownloadJobRow MapToRow(DownloadJob job)
    {
        return new DownloadJobRow
        {
            Id = job.Id.ToString(),
            Url = job.Source.Url.ToString(),
            Platform = (int)job.Source.Platform,
            Title = job.Source.Title,
            Uploader = job.Source.Uploader,
            DurationTicks = job.Source.Duration.Ticks,
            ThumbnailUrl = job.Source.ThumbnailUrl,
            OutputFormat = (int)job.OutputFormat,
            Status = (int)job.Status,
            OutputPath = job.OutputPath?.Value,
            MetadataJson = job.Metadata != null ? JsonSerializer.Serialize(job.Metadata) : null,
            CreatedAt = job.CreatedAt.ToString("o"),
            CompletedAt = job.CompletedAt?.ToString("o"),
            ErrorMessage = job.ErrorMessage
        };
    }

    private static DownloadJob MapToEntity(DownloadJobRow row)
    {
        var source = new MediaSource
        {
            Url = new Uri(row.Url),
            Platform = (Platform)row.Platform,
            Title = row.Title,
            Uploader = row.Uploader,
            Duration = TimeSpan.FromTicks(row.DurationTicks),
            ThumbnailUrl = row.ThumbnailUrl
        };

        var job = new DownloadJob
        {
            Source = source,
            OutputFormat = (OutputFormat)row.OutputFormat
        };

        SetProperty(job, nameof(job.Id), Guid.Parse(row.Id));
        SetProperty(job, nameof(job.CreatedAt), DateTime.Parse(row.CreatedAt));

        if (!string.IsNullOrEmpty(row.CompletedAt))
        {
            SetProperty(job, nameof(job.CompletedAt), DateTime.Parse(row.CompletedAt));
        }

        if (!string.IsNullOrEmpty(row.OutputPath))
        {
            job.MarkConverted(new FilePath(row.OutputPath));
        }

        if (!string.IsNullOrEmpty(row.MetadataJson))
        {
            try
            {
                var metadata = JsonSerializer.Deserialize<MediaMetadata>(row.MetadataJson);
                if (metadata != null)
                {
                    job.AttachMetadata(metadata);
                }
            }
            catch
            {
                // Ignore corrupt JSON metadata gracefully
            }
        }

        SetProperty(job, nameof(job.Status), (DownloadStatus)row.Status);
        SetProperty(job, nameof(job.ErrorMessage), row.ErrorMessage);

        return job;
    }

    private static void SetProperty<TEntity, TValue>(TEntity entity, string propertyName, TValue value)
    {
        var prop = typeof(TEntity).GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (prop != null && prop.CanWrite)
        {
            prop.SetValue(entity, value);
        }
        else if (prop != null)
        {
            var field = typeof(TEntity).GetField($"<{propertyName}>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
            field?.SetValue(entity, value);
        }
    }

    private class DownloadJobRow
    {
        public string Id { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public int Platform { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Uploader { get; set; } = string.Empty;
        public long DurationTicks { get; set; }
        public string? ThumbnailUrl { get; set; }
        public int OutputFormat { get; set; }
        public int Status { get; set; }
        public string? OutputPath { get; set; }
        public string? MetadataJson { get; set; }
        public string CreatedAt { get; set; } = string.Empty;
        public string? CompletedAt { get; set; }
        public string? ErrorMessage { get; set; }
    }
}