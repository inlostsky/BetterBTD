using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BetterBTD.Models;
using BetterBTD.Services;

namespace BetterBTD.ViewModels;

public sealed class AutoTasksPageViewModel : ObservableObject
{
    private readonly LocalizationService _localizationService;

    public AutoTasksPageViewModel()
    {
        _localizationService = LocalizationService.Instance;

        Tasks =
        [
            new AutoTaskConfig { Key = "custom" },
            new AutoTaskConfig { Key = "collection" },
            new AutoTaskConfig { Key = "blackborder" },
            new AutoTaskConfig { Key = "race" }
        ];

        ToggleTaskCommand = new RelayCommand<AutoTaskConfig?>(ToggleTask);
        OpenTutorialCommand = new RelayCommand<AutoTaskConfig?>(OpenTutorial);

        _localizationService.LanguageChanged += (_, _) => RefreshLocalizedContent();
        RefreshLocalizedContent();
    }

    public ObservableCollection<AutoTaskConfig> Tasks { get; }

    public IRelayCommand<AutoTaskConfig?> ToggleTaskCommand { get; }

    public IRelayCommand<AutoTaskConfig?> OpenTutorialCommand { get; }

    public string TutorialLinkText => _localizationService.T("Tasks.Tutorial");

    public string OperationIntervalLabel => _localizationService.T("Tasks.OperationInterval");

    public string OperationIntervalDescription => _localizationService.T("Tasks.OperationIntervalDesc");

    public string MapLabel => _localizationService.T("Tasks.MapLabel");

    public string MapDescription => _localizationService.T("Tasks.MapDescription");

    public string DifficultyLabel => _localizationService.T("Tasks.DifficultyLabel");

    public string DifficultyDescription => _localizationService.T("Tasks.DifficultyDescription");

    public string ModeLabel => _localizationService.T("Tasks.ModeLabel");

    public string ModeDescription => _localizationService.T("Tasks.ModeDescription");

    private void ToggleTask(AutoTaskConfig? task)
    {
        if (task is null)
        {
            return;
        }

        task.IsRunning = !task.IsRunning;
        task.RunningButtonText = task.IsRunning ? _localizationService.T("Tasks.Stop") : _localizationService.T("Tasks.Start");
    }

    private void OpenTutorial(AutoTaskConfig? task)
    {
        if (task is null || string.IsNullOrWhiteSpace(task.TutorialUrl))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = task.TutorialUrl,
            UseShellExecute = true
        });
    }

    private void RefreshLocalizedContent()
    {
        var mapOptions = BuildMapOptions();
        var difficultyOptions = BuildDifficultyOptions();
        var modeOptions = BuildModeOptions();

        foreach (var task in Tasks)
        {
            task.Title = _localizationService.T($"Tasks.{task.Key}.Title");
            task.Description = _localizationService.T($"Tasks.{task.Key}.Description");
            task.TutorialUrl = _localizationService.T("Tasks.TutorialUrl");
            task.RunningButtonText = task.IsRunning ? _localizationService.T("Tasks.Stop") : _localizationService.T("Tasks.Start");

            task.MapOptions = new ObservableCollection<string>(mapOptions);
            task.DifficultyOptions = new ObservableCollection<string>(difficultyOptions);
            task.ModeOptions = new ObservableCollection<string>(modeOptions);

            task.SelectedMap = task.MapOptions.FirstOrDefault() ?? string.Empty;
            task.SelectedDifficulty = task.DifficultyOptions.FirstOrDefault() ?? string.Empty;
            task.SelectedMode = task.ModeOptions.FirstOrDefault() ?? string.Empty;
        }

        OnPropertyChanged(nameof(TutorialLinkText));
        OnPropertyChanged(nameof(OperationIntervalLabel));
        OnPropertyChanged(nameof(OperationIntervalDescription));
        OnPropertyChanged(nameof(MapLabel));
        OnPropertyChanged(nameof(MapDescription));
        OnPropertyChanged(nameof(DifficultyLabel));
        OnPropertyChanged(nameof(DifficultyDescription));
        OnPropertyChanged(nameof(ModeLabel));
        OnPropertyChanged(nameof(ModeDescription));
    }

    private IReadOnlyList<string> BuildMapOptions() =>
    [
        _localizationService.T("Tasks.Map.BeginnersTrack"),
        _localizationService.T("Tasks.Map.MonkeyMeadow"),
        _localizationService.T("Tasks.Map.DarkCastle")
    ];

    private IReadOnlyList<string> BuildDifficultyOptions() =>
    [
        _localizationService.T("Tasks.Difficulty.Easy"),
        _localizationService.T("Tasks.Difficulty.Medium"),
        _localizationService.T("Tasks.Difficulty.Hard")
    ];

    private IReadOnlyList<string> BuildModeOptions() =>
    [
        _localizationService.T("Tasks.Mode.Standard"),
        _localizationService.T("Tasks.Mode.Deflation"),
        _localizationService.T("Tasks.Mode.Chimps")
    ];
}
