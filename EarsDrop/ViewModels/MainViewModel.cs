using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EarsDrop.Services;

namespace EarsDrop.ViewModels;
public partial class MainViewModel : ViewModelBase
{
    private readonly INavigationService _navigation;
    public QueueViewModel Queue { get; }
    public DownloadHistoryViewModel History { get; }
    public SettingsViewModel Settings { get; }
    public AboutViewModel About { get; }
    [ObservableProperty] private ViewModelBase _currentPage;
    public MainViewModel(INavigationService navigation, QueueViewModel queue, DownloadHistoryViewModel history, SettingsViewModel settings, AboutViewModel about)
    { _navigation = navigation; Queue = queue; History = history; Settings = settings; About = about; _currentPage = queue; _navigation.ViewChanged += OnNavigation; }
    private void OnNavigation(object? sender, AppView view) => CurrentPage = view switch { AppView.History => History, AppView.Settings => Settings, _ => Queue };
    [RelayCommand] private void ShowQueue() => _navigation.NavigateTo(AppView.Queue);
    [RelayCommand] private void ShowHistory() => _navigation.NavigateTo(AppView.History);
    [RelayCommand] private void ShowSettings() => _navigation.NavigateTo(AppView.Settings);
    [RelayCommand] private void ShowAbout() => CurrentPage = About;
}
