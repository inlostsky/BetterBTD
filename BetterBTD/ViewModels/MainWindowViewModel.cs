using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using BetterBTD.Models;
using BetterBTD.Services;

namespace BetterBTD.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private readonly LocalizationService _localizationService;

    public MainWindowViewModel()
    {
        var configurationService = ConfigurationService.Instance;
        _localizationService = LocalizationService.Instance;

        Configuration = configurationService.Current;
        _localizationService.SetLanguage(Configuration.LanguageCode);

        NavigationItems = [];
        BuildNavigationItems();

        _localizationService.LanguageChanged += (_, _) => BuildNavigationItems();
    }

    public ObservableCollection<NavigationItem> NavigationItems { get; }

    public AppConfiguration Configuration { get; }

    private void BuildNavigationItems()
    {
        NavigationItems.Clear();
        NavigationItems.Add(new NavigationItem("start", "Home24", _localizationService.T("Nav.Start.Title"), _localizationService.T("Nav.Start.Description")));
        NavigationItems.Add(new NavigationItem("tasks", "PlayCircle24", _localizationService.T("Nav.Tasks.Title"), _localizationService.T("Nav.Tasks.Description")));
        NavigationItems.Add(new NavigationItem("editor", "Edit24", _localizationService.T("Nav.Editor.Title"), _localizationService.T("Nav.Editor.Description")));
        NavigationItems.Add(new NavigationItem("library", "Folder24", _localizationService.T("Nav.Library.Title"), _localizationService.T("Nav.Library.Description")));
        NavigationItems.Add(new NavigationItem("settings", "Settings24", _localizationService.T("Nav.Settings.Title"), _localizationService.T("Nav.Settings.Description")));
        NavigationItems.Add(new NavigationItem("logs", "Document24", _localizationService.T("Nav.Logs.Title"), _localizationService.T("Nav.Logs.Description")));
    }
}
