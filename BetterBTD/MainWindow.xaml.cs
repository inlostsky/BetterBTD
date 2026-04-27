using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
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
            this.ContentRendered += MainWindow_ContentRendered;
        }
        private void MainWindow_ContentRendered(object sender, EventArgs e)
        {
            RootNavigation.Navigate(typeof(Views.Pages.StartPageView));
        }
    }
}