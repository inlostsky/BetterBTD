using System.Windows.Controls;
using BetterBTD.Services;
using BetterBTD.ViewModels;

namespace BetterBTD.Views.Pages;

public partial class MyScriptsPageView : Page
{
    public MyScriptsPageView()
    {
        InitializeComponent();
        DataContext = new MyScriptsPageViewModel(LocalizationService.Instance);
    }
}
