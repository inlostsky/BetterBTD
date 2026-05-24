using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BetterBTD.ViewModels;

public sealed partial class TextFileEditorWindowViewModel : ObservableObject
{
    private readonly LocalizationService _localizationService;
    private readonly AppDialogService _appDialogService;
    private readonly Action _closeWindow;

    [ObservableProperty]
    private string _windowTitle = string.Empty;

    [ObservableProperty]
    private string _documentTitle = string.Empty;

    [ObservableProperty]
    private string _filePath = string.Empty;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private bool _isDirty;

    [ObservableProperty]
    private bool _isEditorReady;

    private string _editorText = string.Empty;

    public TextFileEditorWindowViewModel(
        LocalizationService localizationService,
        AppDialogService appDialogService,
        string filePath,
        Action closeWindow)
    {
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _appDialogService = appDialogService ?? throw new ArgumentNullException(nameof(appDialogService));
        _closeWindow = closeWindow ?? throw new ArgumentNullException(nameof(closeWindow));

        FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        DocumentTitle = Path.GetFileName(filePath);
        WindowTitle = $"{_localizationService.T("TextEditor.WindowTitle")} - {DocumentTitle}";
        StatusText = _localizationService.T("TextEditor.Status.Loading");

        SaveCommand = new RelayCommand(Save, CanSave);
        ReloadCommand = new RelayCommand(Reload, CanReload);
        CloseCommand = new RelayCommand(Close);

        Reload();
    }

    public IRelayCommand SaveCommand { get; }

    public IRelayCommand ReloadCommand { get; }

    public IRelayCommand CloseCommand { get; }

    public string EditorText => _editorText;

    public string SaveText => _localizationService.T("TextEditor.Menu.Save");

    public string ReloadText => _localizationService.T("TextEditor.Menu.Reload");

    public string CloseText => _localizationService.T("TextEditor.Menu.Close");

    partial void OnIsDirtyChanged(bool value)
    {
        SaveCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(DocumentTitleText));
    }

    partial void OnIsEditorReadyChanged(bool value)
    {
        SaveCommand.NotifyCanExecuteChanged();
        ReloadCommand.NotifyCanExecuteChanged();
    }

    public string DocumentTitleText => IsDirty ? $"{DocumentTitle} *" : DocumentTitle;

    public void SetEditorReady()
    {
        IsEditorReady = true;
        StatusText = _localizationService.T("TextEditor.Status.Ready");
        OnPropertyChanged(nameof(EditorText));
    }

    public void UpdateTextFromEditor(string text)
    {
        _editorText = text ?? string.Empty;
        IsDirty = !string.Equals(_editorText, SafeReadAllText(FilePath), StringComparison.Ordinal);
        StatusText = IsDirty
            ? _localizationService.T("TextEditor.Status.Modified")
            : _localizationService.T("TextEditor.Status.Ready");
    }

    public void HandleSaveSucceeded()
    {
        IsDirty = false;
        StatusText = _localizationService.T("TextEditor.Status.Saved");
    }

    public void HandleEditorError(string message)
    {
        StatusText = string.Format(_localizationService.T("TextEditor.Status.Error"), message);
    }

    public bool ConfirmClose()
    {
        if (!IsDirty)
        {
            return true;
        }

        var request = new AppDialogRequest
        {
            Title = _localizationService.T("TextEditor.Unsaved.Title"),
            Message = _localizationService.T("TextEditor.Unsaved.Message"),
            PrimaryButtonText = _localizationService.T("TextEditor.Unsaved.Save"),
            SecondaryButtonText = _localizationService.T("TextEditor.Unsaved.Discard"),
            CloseButtonText = _localizationService.T("TextEditor.Unsaved.Cancel")
        };

        var result = _appDialogService.Show(request);
        if (result == AppDialogResult.Primary)
        {
            Save();
            return !IsDirty;
        }

        return result == AppDialogResult.Secondary;
    }

    private bool CanSave()
    {
        return IsEditorReady && IsDirty;
    }

    private bool CanReload()
    {
        return IsEditorReady;
    }

    private void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(FilePath, _editorText);
            HandleSaveSucceeded();
        }
        catch (Exception ex)
        {
            _appDialogService.Show(new AppDialogRequest
            {
                Title = _localizationService.T("TextEditor.SaveError.Title"),
                Message = string.Format(_localizationService.T("TextEditor.SaveError.Message"), ex.Message),
                PrimaryButtonText = _localizationService.T("TextEditor.Dialog.Ok")
            });
            StatusText = string.Format(_localizationService.T("TextEditor.Status.Error"), ex.Message);
        }
    }

    private void Reload()
    {
        _editorText = SafeReadAllText(FilePath);
        IsDirty = false;
        StatusText = _localizationService.T("TextEditor.Status.Ready");
        OnPropertyChanged(nameof(EditorText));
    }

    private void Close()
    {
        _closeWindow();
    }

    private static string SafeReadAllText(string filePath)
    {
        return File.Exists(filePath) ? File.ReadAllText(filePath) : string.Empty;
    }
}
