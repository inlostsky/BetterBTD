using System;
using System.Windows;
using System.Windows.Input;
using BetterBTD.Core.Config;
using BetterBTD.Services;
using Fischless.WindowsInput;
using TextBox = Wpf.Ui.Controls.TextBox;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseButton = System.Windows.Input.MouseButton;

namespace BetterBTD.View.Controls.KeyBindings;

public class KeyBindingTextBox : TextBox
{
    public static readonly DependencyProperty KeyBindingProperty = DependencyProperty.Register(
        nameof(KeyBinding),
        typeof(HotkeyBinding),
        typeof(KeyBindingTextBox),
        new FrameworkPropertyMetadata(
            new HotkeyBinding(),
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnKeyBindingChanged));

    /// <summary>
    /// 按键绑定
    /// </summary>
    public HotkeyBinding KeyBinding
    {
        get => (HotkeyBinding)GetValue(KeyBindingProperty);
        set => SetValue(KeyBindingProperty, value);
    }

    public KeyBindingTextBox()
    {
        IsReadOnly = true;
        IsReadOnlyCaretVisible = false;
        IsUndoEnabled = false;

        if (ContextMenu is not null)
        {
            ContextMenu.Visibility = Visibility.Collapsed;
        }

        Text = KeyBinding.DisplayName;
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        var key = e.Key;
        if (key == Key.System)
        {
            key = e.SystemKey;
        }

        KeyBinding = new HotkeyBinding
        {
            Modifiers = Keyboard.Modifiers,
            Key = KeyIdConverter.FromInputKey(key)
        };

        e.Handled = true;
    }

    protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
    {
        var key = e.ChangedButton;

        if (key is MouseButton.Left && !IsFocused)
        {
            return;
        }

        KeyBinding = new HotkeyBinding
        {
            Modifiers = Keyboard.Modifiers,
            Key = KeyIdConverter.FromMouseButton(key)
        };

        e.Handled = true;
    }

    protected override void OnGotFocus(RoutedEventArgs e)
    {
        Text = LocalizationService.Instance.T("Settings.KeyBindings.Editor.Waiting");
        base.OnGotFocus(e);
    }

    protected override void OnLostFocus(RoutedEventArgs e)
    {
        Text = KeyBinding.DisplayName;
        base.OnLostFocus(e);
    }

    private static void OnKeyBindingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (KeyBindingTextBox)d;
        control.Text = (e.NewValue as HotkeyBinding)?.DisplayName ?? string.Empty;
    }
}
