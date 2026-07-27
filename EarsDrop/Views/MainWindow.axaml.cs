using Avalonia.Controls;
using EarsDrop.ViewModels;

namespace EarsDrop.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}