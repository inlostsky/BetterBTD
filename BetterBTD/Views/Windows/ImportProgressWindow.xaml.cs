using System.ComponentModel;
using BetterBTD.Services.Shared;
using Wpf.Ui.Controls;

namespace BetterBTD.Views.Windows;

public partial class ImportProgressWindow : FluentWindow
{
    private bool _allowClose;

    public ImportProgressWindow(ImportProgressDialogRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        InitializeComponent();

        Title = request.Title;
        DialogTitleBar.Title = request.Title;
        MessageTextBlock.Text = request.Message;
    }

    public void UpdateMessage(string message)
    {
        MessageTextBlock.Text = message ?? string.Empty;
    }

    public void CloseDialog()
    {
        _allowClose = true;
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            return;
        }

        base.OnClosing(e);
    }
}
