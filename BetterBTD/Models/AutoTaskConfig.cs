using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BetterBTD.Models;

public partial class AutoTaskConfig : ObservableObject
{
    [ObservableProperty]
    private string _key = string.Empty;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _tutorialUrl = string.Empty;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private string _runningButtonText = string.Empty;

    [ObservableProperty]
    private int _operationIntervalMs = 200;

    [ObservableProperty]
    private ObservableCollection<string> _mapOptions = [];

    [ObservableProperty]
    private string _selectedMap = string.Empty;

    [ObservableProperty]
    private ObservableCollection<string> _difficultyOptions = [];

    [ObservableProperty]
    private string _selectedDifficulty = string.Empty;

    [ObservableProperty]
    private ObservableCollection<string> _modeOptions = [];

    [ObservableProperty]
    private string _selectedMode = string.Empty;
}
