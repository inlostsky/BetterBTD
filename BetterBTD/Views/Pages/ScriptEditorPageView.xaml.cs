using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using BetterBTD.Services;
using BetterBTD.ViewModels;

namespace BetterBTD.Views.Pages;

public partial class ScriptEditorPageView : Page
{
    private const double EditorMaxHeightOffset = 56d;
    private Window? _hostWindow;

    public static ScriptEditorPageView? Current { get; private set; }

    public ScriptEditorPageViewModel ViewModel => (ScriptEditorPageViewModel)DataContext;

    public ScriptEditorPageView()
    {
        InitializeComponent();
        DataContext = new ScriptEditorPageViewModel(LocalizationService.Instance);

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Current = this;
        _hostWindow = Window.GetWindow(this);
        if (_hostWindow is null)
        {
            return;
        }

        _hostWindow.SizeChanged += OnHostWindowSizeChanged;
        UpdateMaxHeightFromHostWindow();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (ReferenceEquals(Current, this))
        {
            Current = null;
        }

        if (_hostWindow is null)
        {
            return;
        }

        _hostWindow.SizeChanged -= OnHostWindowSizeChanged;
        _hostWindow = null;
    }

    private void OnHostWindowSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateMaxHeightFromHostWindow();
    }

    private void UpdateMaxHeightFromHostWindow()
    {
        if (_hostWindow is null)
        {
            return;
        }

        if (!Dispatcher.CheckAccess())
        {
            _ = Dispatcher.InvokeAsync(UpdateMaxHeightFromHostWindow, DispatcherPriority.Loaded);
            return;
        }

        var maxHeight = _hostWindow.ActualHeight - EditorMaxHeightOffset;
        MaxHeight = maxHeight > 0 ? maxHeight : 0;
    }
}
