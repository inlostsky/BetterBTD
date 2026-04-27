using System.Windows.Controls;
using BetterBTD.Services;
using BetterBTD.ViewModels;

namespace BetterBTD.Views.Pages;

public partial class AutoTasksPageView : Page
{
    public AutoTasksPageView()
    {
        InitializeComponent();
        DataContext = new AutoTasksPageViewModel();
    }
}
