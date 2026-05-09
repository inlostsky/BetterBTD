using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using BetterBTD.ViewModels;
using Wpf.Ui.Controls;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace BetterBTD.Views.Windows;

public partial class ScriptExecutionWindow : FluentWindow
{
    private int _lastLogTextLength;

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

    private void OnLogTextBoxLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not WpfTextBox textBox)
        {
            return;
        }

        _lastLogTextLength = textBox.Text.Length;
        textBox.CaretIndex = textBox.Text.Length;
        textBox.ScrollToEnd();
    }

    private void OnLogTextBoxTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not WpfTextBox textBox)
        {
            return;
        }

        var shouldAutoScroll = !textBox.IsKeyboardFocusWithin ||
                               (textBox.SelectionLength == 0 && textBox.CaretIndex >= _lastLogTextLength);

        _lastLogTextLength = textBox.Text.Length;

        if (!shouldAutoScroll)
        {
            return;
        }

        textBox.CaretIndex = textBox.Text.Length;
        textBox.ScrollToEnd();
    }
}
