using System.Windows.Controls;
using BetterBTD.ViewModels;

namespace BetterBTD.Views.Pages;

public partial class ToolsPageView : Page
{
    public ToolsPageView()
    {
        InitializeComponent();
        DataContext = new ToolsPageViewModel();
    }
}
