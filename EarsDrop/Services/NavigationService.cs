using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace EarsDrop.Services;

public enum AppView
{
    Queue,
    History,
    Settings
}

public interface INavigationService
{
    AppView CurrentView { get; }
    event EventHandler<AppView>? ViewChanged;
    void NavigateTo(AppView view);
}

public class NavigationService : ObservableObject, INavigationService
{
    private AppView _currentView = AppView.Queue;

    public AppView CurrentView
    {
        get => _currentView;
        private set => SetProperty(ref _currentView, value);
    }

    public event EventHandler<AppView>? ViewChanged;

    public void NavigateTo(AppView view)
    {
        if (CurrentView != view)
        {
            CurrentView = view;
            ViewChanged?.Invoke(this, view);
        }
    }
}
