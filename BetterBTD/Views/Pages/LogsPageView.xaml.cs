using System.Windows.Controls;
using BetterBTD.Services;
using BetterBTD.ViewModels;

namespace BetterBTD.Views.Pages;

public partial class LogsPageView : Page
{
    public LogsPageView()
    {
        InitializeComponent();
        DataContext = new LogsPageViewModel(LocalizationService.Instance);
    }
}
