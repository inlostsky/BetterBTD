using System;
using System.Windows;
using System.Windows.Controls;
using BetterBTD.Services;
using BetterBTD.ViewModels;

namespace BetterBTD.Views.Pages;

public partial class MyScriptsPageView : Page
{
    private readonly AppDialogService _appDialogService = AppDialogService.Instance;
    private readonly ManagedScriptLibraryService _managedScriptLibraryService = ManagedScriptLibraryService.Instance;

    public MyScriptsPageView()
    {
        InitializeComponent();
        DataContext = new MyScriptsPageViewModel(LocalizationService.Instance);
    }

    private void EditSelectedScriptButton_OnClick(object sender, RoutedEventArgs e)
    {
        OpenSelectedScriptInEditor();
    }

    private void RunSelectedScriptButton_OnClick(object sender, RoutedEventArgs e)
    {
        RunSelectedScript();
    }

    private void OpenSelectedScriptInEditor()
    {
        if (!TryResolveSelectedScriptFilePath(out var filePath))
        {
            return;
        }

        if (Window.GetWindow(this) is not MainWindow mainWindow)
        {
            ShowOpenScriptError(LocalizationService.Instance.T("Library.Dialog.OpenError.NavigationUnavailable"));
            return;
        }

        mainWindow.NavigateToScriptEditor(filePath);
    }

    private void RunSelectedScript()
    {
        if (!TryResolveSelectedScriptFilePath(out var filePath))
        {
            return;
        }

        _ = ScriptEditorPageViewModel.TryRunScriptFromExternal(filePath, LocalizationService.Instance);
    }

    private bool TryResolveSelectedScriptFilePath(out string filePath)
    {
        if (DataContext is not MyScriptsPageViewModel viewModel || viewModel.SelectedScript is null)
        {
            filePath = string.Empty;
            return false;
        }

        if (_managedScriptLibraryService.TryGetManagedScriptFilePath(viewModel.SelectedScript.ScriptId, out filePath))
        {
            return true;
        }

        ShowOpenScriptError(LocalizationService.Instance.T("Library.Dialog.OpenError.ScriptMissing"));
        filePath = string.Empty;
        return false;
    }

    private void ShowOpenScriptError(string message)
    {
        var localizationService = LocalizationService.Instance;
        _ = _appDialogService.Show(new AppDialogRequest
        {
            Title = localizationService.T("Editor.File.OpenError.Title"),
            Message = string.Format(localizationService.T("Editor.File.OpenError.Message"), message),
            PrimaryButtonText = localizationService.T("Editor.Dialog.Ok")
        });
    }
}
