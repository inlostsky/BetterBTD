using System.Windows.Controls;
using BetterBTD.ViewModels;

namespace BetterBTD.Views.Pages;

public partial class ToolsPageView : Page
{
    public ToolsPageView()
    {
        InitializeComponent();
        DataContext = new ToolsPageViewModel();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is ToolsPageViewModel viewModel)
        {
            viewModel.Resume();
        }
    }

    private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
