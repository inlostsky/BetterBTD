using System.Windows.Input;
using BetterBTD.ViewModels;
using Wpf.Ui.Controls;

namespace BetterBTD.Views.Windows;

public partial class ScriptTagEditorWindow : FluentWindow
{
    public ScriptTagEditorWindow(ScriptEditorPageViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
    }
}
