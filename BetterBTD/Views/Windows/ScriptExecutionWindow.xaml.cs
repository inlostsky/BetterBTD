using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using BetterBTD.ViewModels;
using Wpf.Ui.Controls;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace BetterBTD.Views.Windows;

public partial class ScriptExecutionWindow : FluentWindow
{
    private const int HotkeyId = 0xB10;
    private const int WmHotkey = 0x0312;

    private int _lastLogTextLength;
    private HwndSource? _hwndSource;
    private bool _isGlobalHotkeyRegistered;

    public ScriptExecutionWindow(ScriptExecutionWindowViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();
        DataContext = viewModel;
        SourceInitialized += OnSourceInitialized;
        Closing += OnClosing;
        Closed += OnClosed;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hwndSource = PresentationSource.FromVisual(this) as HwndSource;
        _hwndSource?.AddHook(WndProc);
        TryRegisterGlobalHotkey();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (DataContext is ScriptExecutionWindowViewModel viewModel)
        {
            viewModel.HandleWindowClosing();
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        ReleaseGlobalHotkey();

        if (_hwndSource is not null)
        {
            _hwndSource.RemoveHook(WndProc);
            _hwndSource = null;
        }

        SourceInitialized -= OnSourceInitialized;
        Closing -= OnClosing;
        Closed -= OnClosed;
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

    private async void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_isGlobalHotkeyRegistered || (e.Key != Key.F10 && e.SystemKey != Key.F10))
        {
            return;
        }

        e.Handled = true;
        await ToggleExecutionAsync();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            handled = true;
            _ = Dispatcher.InvokeAsync(ToggleExecutionAsync);
        }

        return IntPtr.Zero;
    }

    private void TryRegisterGlobalHotkey()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var virtualKey = (uint)KeyInterop.VirtualKeyFromKey(Key.F10);
        if (RegisterHotKey(handle, HotkeyId, 0, virtualKey))
        {
            _isGlobalHotkeyRegistered = true;
            return;
        }

        _isGlobalHotkeyRegistered = false;
        var errorCode = Marshal.GetLastWin32Error();
        System.Windows.MessageBox.Show(
            this,
            $"F10 global hotkey registration failed (Win32: {errorCode}). The hotkey may already be in use by another application.",
            "BetterBTD",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Warning);
    }

    private void ReleaseGlobalHotkey()
    {
        if (!_isGlobalHotkeyRegistered)
        {
            return;
        }

        var handle = new WindowInteropHelper(this).Handle;
        if (handle != IntPtr.Zero)
        {
            _ = UnregisterHotKey(handle, HotkeyId);
        }

        _isGlobalHotkeyRegistered = false;
    }

    private async Task ToggleExecutionAsync()
    {
        if (DataContext is not ScriptExecutionWindowViewModel viewModel)
        {
            return;
        }

        if (viewModel.StopCommand.CanExecute(null))
        {
            viewModel.StopCommand.Execute(null);
            return;
        }

        if (viewModel.StartCommand.CanExecute(null))
        {
            await viewModel.StartCommand.ExecuteAsync(null);
        }
    }
}
