using System.ComponentModel;
using BetterBTD.ViewModels;
using Wpf.Ui.Controls;

namespace BetterBTD.Views.Windows;

public partial class ScriptExecutionWindow : FluentWindow
{
    public ScriptExecutionWindow(ScriptExecutionWindowViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();
        DataContext = viewModel;
        Closing += OnClosing;
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (DataContext is ScriptExecutionWindowViewModel viewModel)
        {
            viewModel.HandleWindowClosing();
        }
    }
}
