using System.Collections.ObjectModel;
using Application.UseCases.DeleteDownload;
using Application.UseCases.GetDownloadHistory;
using Application.UseCases.RetryDownload;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EarsDrop.Services;
using MediatR;

namespace EarsDrop.ViewModels;
public partial class DownloadHistoryViewModel : ViewModelBase
{
    private readonly ISender _sender;
    private readonly INotificationService _notifications;
    public ObservableCollection<DownloadCardViewModel> Items { get; } = [];
    [ObservableProperty] private string searchText = string.Empty;
    [ObservableProperty] private string sortBy = "Newest";
    public string[] SortOptions { get; } = ["Newest", "Title", "Artist"];
    public DownloadHistoryViewModel(ISender sender, INotificationService notifications) { _sender = sender; _notifications = notifications; }
    [RelayCommand] public async Task RefreshAsync()
    { var result = await _sender.Send(new GetDownloadHistoryQuery()); if (result.IsSuccess) { Items.Clear(); foreach (var item in result.Value.OrderByDescending(x => x.CreatedAt).Where(Matches)) Items.Add(DownloadCardViewModel.FromDto(item)); } }
    private bool Matches(Application.DTOs.DownloadJobDto dto) => string.IsNullOrWhiteSpace(SearchText) || dto.Source.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase) || dto.Source.Uploader.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
    [RelayCommand] private async Task DeleteAsync(DownloadCardViewModel item) { var result = await _sender.Send(new DeleteDownloadCommand(item.Id)); if (result.IsSuccess) Items.Remove(item); }
    [RelayCommand] private async Task RetryAsync(DownloadCardViewModel item)
    { var result = await _sender.Send(new RetryDownloadCommand(item.Id)); if (result.IsSuccess) { var index = Items.IndexOf(item); Items[index] = DownloadCardViewModel.FromDto(result.Value); } }
    [RelayCommand] private void OpenFolder(DownloadCardViewModel item) { if (!string.IsNullOrWhiteSpace(item.OutputPath)) _notifications.OpenFileLocation(item.OutputPath); }
    partial void OnSearchTextChanged(string value) => _ = RefreshAsync();
}
