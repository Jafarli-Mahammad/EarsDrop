using Domain.Common;
using Domain.Enums;
using Domain.Value_Objects;

namespace Domain.Entities;

public class DownloadJob : Entity
{
    public MediaSource Source { get; set; } = default!;

    public OutputFormat OutputFormat { get; set; }

    public DownloadStatus Status { get; private set; } = DownloadStatus.Pending;

    public FilePath? OutputPath { get; private set; }

    public MediaMetadata? Metadata { get; private set; }

    public DateTime CreatedAt { get; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; private set; }

    public string? ErrorMessage { get; private set; }

    public void Start()
    {
        Status = DownloadStatus.Downloading;
    }

    public void MarkConverted(FilePath outputPath)
    {
        OutputPath = outputPath;
        Status = DownloadStatus.Converting;
    }

    public void FetchMetadata()
    {
        Status = DownloadStatus.FetchingMetadata;
    }

    public void AttachMetadata(MediaMetadata metadata)
    {
        Metadata = metadata;
        Status = DownloadStatus.WritingMetadata;
    }

    public void Complete()
    {
        Status = DownloadStatus.Completed;
        CompletedAt = DateTime.UtcNow;
    }

    public void Fail(string error)
    {
        Status = DownloadStatus.Failed;
        ErrorMessage = error;
    }

    public void Cancel()
    {
        Status = DownloadStatus.Cancelled;
    }
}