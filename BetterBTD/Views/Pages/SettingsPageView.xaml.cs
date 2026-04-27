using System.Windows.Controls;
using BetterBTD.Services;
using BetterBTD.ViewModels;

namespace BetterBTD.Views.Pages;

public partial class SettingsPageView : Page
{
    public SettingsPageView()
    {
        InitializeComponent();
        DataContext = new SettingsPageViewModel();
    }
}
