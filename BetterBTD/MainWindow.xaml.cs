using System;
using System.Windows;
using System.Windows.Threading;
using BetterBTD.ViewModels;
using Wpf.Ui.Controls;

namespace BetterBTD
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : FluentWindow
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainWindowViewModel();
            ContentRendered += MainWindow_ContentRendered;
        }

        private void MainWindow_ContentRendered(object sender, EventArgs e)
        {
            RootNavigation.Navigate(typeof(Views.Pages.StartPageView));
        }

        public void NavigateToScriptEditor(string scriptFilePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(scriptFilePath);

            RootNavigation.Navigate(typeof(Views.Pages.ScriptEditorPageView));

            _ = Dispatcher.InvokeAsync(
                () =>
                {
                    var editorPage = Views.Pages.ScriptEditorPageView.Current;
                    if (editorPage is null)
                    {
                        return;
                    }

                    _ = editorPage.ViewModel.TryOpenScriptFromExternal(scriptFilePath, openRuntimeWindow: false);
                },
                DispatcherPriority.Loaded);
        }
    }
}
