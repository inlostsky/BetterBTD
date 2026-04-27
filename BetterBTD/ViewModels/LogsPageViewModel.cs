using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using BetterBTD.Models;
using BetterBTD.Services;

namespace BetterBTD.ViewModels;

public sealed class LogsPageViewModel : ObservableObject
{
    private readonly LocalizationService _localizationService;
    private bool _liveModeEnabled = true;

    public LogsPageViewModel(LocalizationService localizationService)
    {
        _localizationService = localizationService;
        _localizationService.LanguageChanged += (_, _) => RefreshLocalizedContent();

        Entries = [];
        RefreshLocalizedContent();
    }

    public ObservableCollection<LogEntry> Entries { get; }

    public bool LiveModeEnabled
    {
        get => _liveModeEnabled;
        set
        {
            if (SetProperty(ref _liveModeEnabled, value))
            {
                OnPropertyChanged(nameof(LiveModeText));
            }
        }
    }

    public string Title => _localizationService.T("Logs.Title");
    public string TimeHeader => _localizationService.T("Logs.Col.Time");
    public string LevelHeader => _localizationService.T("Logs.Col.Level");
    public string MessageHeader => _localizationService.T("Logs.Col.Message");
    public string LiveModeText => $"{_localizationService.T("Logs.LiveMode")}{(LiveModeEnabled ? _localizationService.T("Logs.LiveEnabled") : _localizationService.T("Logs.LiveDisabled"))}";

    private void RefreshLocalizedContent()
    {
        Entries.Clear();
        Entries.Add(new LogEntry { Time = "10:30:12", Level = "Info", Message = _localizationService.T("Logs.Init1") });
        Entries.Add(new LogEntry { Time = "10:30:20", Level = "Info", Message = _localizationService.T("Logs.Init2") });
        Entries.Add(new LogEntry { Time = "10:31:05", Level = "Warn", Message = _localizationService.T("Logs.Init3") });

        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(TimeHeader));
        OnPropertyChanged(nameof(LevelHeader));
        OnPropertyChanged(nameof(MessageHeader));
        OnPropertyChanged(nameof(LiveModeText));
    }
}
