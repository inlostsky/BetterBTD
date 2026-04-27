using BetterBTD.ViewModels;
using Wpf.Ui.Controls;

namespace BetterBTD.Views.Windows;

public partial class KeyBindingsWindow : FluentWindow
{
    public KeyBindingsWindow()
    {
        InitializeComponent();
        DataContext = new KeyBindingsSettingsPageViewModel();
    }
}
