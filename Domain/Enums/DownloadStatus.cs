namespace Domain.Enums;

public enum DownloadStatus
{
    Pending,
    Downloading,
    Converting,
    FetchingMetadata,
    WritingMetadata,
    Completed,
    Failed,
    Cancelled
}