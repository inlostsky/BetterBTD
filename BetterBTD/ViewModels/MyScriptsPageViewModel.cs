using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using BetterBTD.Services;

namespace BetterBTD.ViewModels;

public sealed class MyScriptsPageViewModel : ObservableObject
{
    private readonly LocalizationService _localizationService;
    private string _title = string.Empty;
    private string _enterText = string.Empty;

    public MyScriptsPageViewModel(LocalizationService localizationService)
    {
        _localizationService = localizationService;
        _localizationService.LanguageChanged += (_, _) => RefreshLocalizedContent();

        Categories = [];
        RefreshLocalizedContent();
    }

    public ObservableCollection<string> Categories { get; }

    public string Title
    {
        get => _title;
        private set => SetProperty(ref _title, value);
    }

    public string EnterText
    {
        get => _enterText;
        private set => SetProperty(ref _enterText, value);
    }

    private void RefreshLocalizedContent()
    {
        Title = _localizationService.T("Library.Title");
        EnterText = _localizationService.T("Library.Enter");

        Categories.Clear();
        Categories.Add(_localizationService.T("Library.Category.1"));
        Categories.Add(_localizationService.T("Library.Category.2"));
        Categories.Add(_localizationService.T("Library.Category.3"));
        Categories.Add(_localizationService.T("Library.Category.4"));
    }
}
