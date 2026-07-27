using Application.DTOs;
using CommunityToolkit.Mvvm.ComponentModel;
using Domain.Enums;

namespace EarsDrop.ViewModels;
public partial class DownloadCardViewModel : ViewModelBase
{
    public Guid Id { get; init; } = Guid.NewGuid();
    [ObservableProperty] private string title = "Preparing download…";
    [ObservableProperty] private string artist = "EarsDrop";
    [ObservableProperty] private string? thumbnailUrl;
    [ObservableProperty] private string status = "Queued";
    [ObservableProperty] private double progress;
    [ObservableProperty] private string speed = "Waiting";
    [ObservableProperty] private string eta = "—";
    [ObservableProperty] private string? outputPath;
    [ObservableProperty] private DownloadStatus downloadStatus = DownloadStatus.Pending;
    public bool IsTerminal => DownloadStatus is DownloadStatus.Completed or DownloadStatus.Failed or DownloadStatus.Cancelled;
    public string StatusText => Status;
    public static DownloadCardViewModel FromDto(DownloadJobDto dto) => new()
    { Id = dto.Id, Title = dto.Source.Title, Artist = dto.Metadata?.Artist ?? dto.Source.Uploader, ThumbnailUrl = dto.Source.ThumbnailUrl, OutputPath = dto.OutputPath, DownloadStatus = dto.Status, Status = dto.Status.ToString(), Progress = dto.Status == DownloadStatus.Completed ? 100 : 0, Speed = dto.Status == DownloadStatus.Completed ? "Complete" : "—" };
    partial void OnDownloadStatusChanged(DownloadStatus value) => OnPropertyChanged(nameof(IsTerminal));
}
