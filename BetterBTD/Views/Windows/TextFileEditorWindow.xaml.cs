using System.ComponentModel;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using BetterBTD.ViewModels;
using Microsoft.Web.WebView2.Core;
using Wpf.Ui.Controls;

namespace BetterBTD.Views.Windows;

public partial class TextFileEditorWindow : FluentWindow
{
    private bool _isInitializing;
    private bool _isApplyingEditorContent;

    public TextFileEditorWindow(TextFileEditorWindowViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();
        DataContext = viewModel;
        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    private async void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_isInitializing)
        {
            return;
        }

        _isInitializing = true;

        try
        {
            await EditorWebView.EnsureCoreWebView2Async();
            EditorWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            EditorWebView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            EditorWebView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
            EditorWebView.NavigateToString(BuildEditorHtml());

            if (DataContext is TextFileEditorWindowViewModel viewModel)
            {
                viewModel.PropertyChanged += OnViewModelPropertyChanged;
            }
        }
        catch (Exception ex)
        {
            if (DataContext is TextFileEditorWindowViewModel viewModel)
            {
                viewModel.HandleEditorError(ex.Message);
            }
        }
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (DataContext is not TextFileEditorWindowViewModel viewModel)
        {
            return;
        }

        if (!viewModel.ConfirmClose())
        {
            e.Cancel = true;
            return;
        }

        viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        if (EditorWebView.CoreWebView2 is not null)
        {
            EditorWebView.CoreWebView2.WebMessageReceived -= OnWebMessageReceived;
        }
    }

    private async void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not TextFileEditorWindowViewModel viewModel ||
            EditorWebView.CoreWebView2 is null)
        {
            return;
        }

        if (string.Equals(e.PropertyName, nameof(TextFileEditorWindowViewModel.EditorText), StringComparison.Ordinal))
        {
            await ApplyEditorTextAsync(viewModel.EditorText);
        }
    }

    private async void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (DataContext is not TextFileEditorWindowViewModel viewModel)
        {
            return;
        }

        var payload = e.TryGetWebMessageAsString();
        if (string.IsNullOrWhiteSpace(payload))
        {
            return;
        }

        using var json = JsonDocument.Parse(payload);
        if (!json.RootElement.TryGetProperty("type", out var typeElement))
        {
            return;
        }

        var messageType = typeElement.GetString();
        switch (messageType)
        {
            case "ready":
                viewModel.SetEditorReady();
                await ApplyEditorTextAsync(viewModel.EditorText);
                break;
            case "changed":
                if (_isApplyingEditorContent)
                {
                    return;
                }

                if (json.RootElement.TryGetProperty("text", out var textElement))
                {
                    viewModel.UpdateTextFromEditor(textElement.GetString() ?? string.Empty);
                }
                break;
            case "saved":
                viewModel.HandleSaveSucceeded();
                break;
        }
    }

    private async Task ApplyEditorTextAsync(string text)
    {
        if (EditorWebView.CoreWebView2 is null)
        {
            return;
        }

        _isApplyingEditorContent = true;
        try
        {
            var serialized = JsonSerializer.Serialize(text ?? string.Empty);
            await EditorWebView.ExecuteScriptAsync($"window.setEditorText({serialized});");
        }
        finally
        {
            _isApplyingEditorContent = false;
        }
    }

    private static string BuildEditorHtml()
    {
        var options = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };

        var themeBackground = JsonSerializer.Serialize("#111827", options);
        var themeForeground = JsonSerializer.Serialize("#E5EEF8", options);
        var lineHighlight = JsonSerializer.Serialize("#1F2937", options);
        var selection = JsonSerializer.Serialize("#26415E", options);

        return $$"""
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta http-equiv="Content-Security-Policy" content="default-src 'self' 'unsafe-inline' https://cdnjs.cloudflare.com; connect-src https://cdnjs.cloudflare.com;">
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>BetterBTD Text Editor</title>
    <style>
        html, body, #editor {
            width: 100%;
            height: 100%;
            margin: 0;
            padding: 0;
            overflow: hidden;
            background: #0b1220;
        }
    </style>
    <script src="https://cdnjs.cloudflare.com/ajax/libs/monaco-editor/0.52.2/min/vs/loader.min.js"></script>
</head>
<body>
    <div id="editor"></div>
    <script>
        let editor;
        let suppressChange = false;

        const postMessage = payload => {
            window.chrome.webview.postMessage(JSON.stringify(payload));
        };

        window.setEditorText = function(text) {
            if (!editor) {
                return;
            }

            suppressChange = true;
            const model = editor.getModel();
            if (model) {
                model.setValue(text ?? "");
            }
            suppressChange = false;
        };

        require.config({ paths: { vs: 'https://cdnjs.cloudflare.com/ajax/libs/monaco-editor/0.52.2/min/vs' } });
        require(['vs/editor/editor.main'], function() {
            monaco.editor.defineTheme('betterbtd', {
                base: 'vs-dark',
                inherit: true,
                rules: [],
                colors: {
                    'editor.background': {{themeBackground}},
                    'editor.foreground': {{themeForeground}},
                    'editor.lineHighlightBackground': {{lineHighlight}},
                    'editor.selectionBackground': {{selection}}
                }
            });

            editor = monaco.editor.create(document.getElementById('editor'), {
                value: '',
                language: 'json',
                theme: 'betterbtd',
                automaticLayout: true,
                minimap: { enabled: false },
                fontSize: 14,
                lineNumbers: 'on',
                scrollBeyondLastLine: false,
                tabSize: 2,
                wordWrap: 'on',
                formatOnPaste: true,
                formatOnType: true
            });

            editor.onDidChangeModelContent(function() {
                if (suppressChange) {
                    return;
                }

                postMessage({ type: 'changed', text: editor.getValue() });
            });

            postMessage({ type: 'ready' });
        });
    </script>
</body>
</html>
""";
    }
}
