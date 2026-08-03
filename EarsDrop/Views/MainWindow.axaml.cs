using Avalonia.Controls;
using EarsDrop.ViewModels;

namespace EarsDrop.Views;

public partial class MainWindow : Window
{
    // Parameterless constructor required by the Avalonia XAML runtime loader / designer.
    // The running application always uses the DI constructor below.
    public MainWindow()
    {
        InitializeComponent();
    }

    public MainWindow(MainViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
}